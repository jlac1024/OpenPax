using OpenPax.Sim.Combat;
using OpenPax.Sim.Galaxy;
using OpenPax.Sim.Numerics;
using OpenPax.Sim.Players;

namespace OpenPax.Sim;

/// <summary>
/// Battles: how they start and how a battle with no human side is resolved
/// at once (docs/sim/combat.md). Battles with a human side are fought step
/// by step in the original; until that is ported they resolve the same way.
/// </summary>
public sealed partial class Game
{
    private const int Center = 8;
    private const double JoinDistance = 50.0;

    // Entry points on the 3600 by 3600 field.
    private static readonly (int X, int Y)[] _points =
    [
        (3134, 1800), (3134, 3134), (1800, 3134), (470, 3134), (470, 1800), (470, 470),
        (1800, 470), (3134, 470), (1800, 1800), (2566, 2566), (1500, 1500),
    ];

    // Where each structure kind's orbital stands, by kind 7, 8, 9, 6, 5.
    private static readonly Dictionary<int, (int X, int Y)> _orbitalPoints = new()
    {
        [7] = (3100, 3130),
        [8] = (3308, 2930),
        [9] = (3258, 3130),
        [6] = (3500, 2950),
        [5] = (3058, 3470),
    };

    // Ships are placed by hull type in this order.
    private static readonly int[] _placementOrder = [2, 5, 4, 3, 1, 0];

    /// <summary>Battles waiting, newest first (game +0x9C98). While any exist, strategic time stands still.</summary>
    public List<Battle> Battles { get; } = [];

    public bool InCombat => Battles.Count > 0;

    /// <summary>The next battle object's id (never reset in the original); 0 is never used.</summary>
    public int NextCombatObjectId { get; internal set; } = 1;

    /// <summary>The next battle's number.</summary>
    public int NextBattleId { get; internal set; }

    /// <summary>
    /// How many times leaving ships have gone on toward their exit
    /// (starting at 1, shared by every battle): past 100 the
    /// ship then moving picks its way out again.
    /// </summary>
    public int LeaveCounter { get; internal set; } = 1;

    private int TakeCombatId() => NextCombatObjectId++;

    private StepEvents CombatStep()
    {
        var events = StepEvents.None;
        var battle = Battles[0];
        if (battle.Steps == 0)
        {
            PrimeCommanders(battle);
        }

        battle.Steps++;
        if (battle.Humans > 0 && FightStepByStep)
        {
            TacticalStep(battle, ref events);
            return events;
        }

        AutoResolve(battle, ref events);
        EndBattle(battle, ref events);
        return events;
    }

    /// <summary>The direction slot 0 to 7 of a direction in degrees.</summary>
    private static int Slot(double degrees)
    {
        return X87.ToInt(Orbit.Wrap(degrees - -22.5) * (1.0 / 45.0));
    }

    /// <summary>A fleet attacks a fleet in space: the two, and fleets close by that side with either.</summary>
    internal void StartFleetBattle(Fleet attacker, Fleet target)
    {
        // A mod may take the battle over (docs/modding.md).
        if (TakeOverBattle?.Invoke(this, attacker, target) == true)
        {
            return;
        }

        var battle = new Battle(target.Star) { Id = NextBattleId++ };
        var used = new bool[8];
        var k = Slot(Direction(target.X, target.Y, attacker.X, attacker.Y));
        AddFleet(battle, attacker, _points[k], X87.ToInt(Direction(_points[k].X, _points[k].Y, _points[Center].X, _points[Center].Y)));
        used[k] = true;
        AddFleet(battle, target, _points[Center], X87.ToInt(Direction(_points[Center].X, _points[Center].Y, _points[k].X, _points[k].Y)));

        int Relation(Player side, Fleet f) => f.Owner == side ? Treaty.Allied : side.Treaties[f.Owner.Index].Level();

        foreach (var pass in (int[])[1, 2])
        {
            foreach (var f in Universe.Stars[target.Star].Fleets.ToList())
            {
                if (f.InCombat || Distance(f.X, f.Y, target.X, target.Y) > JoinDistance || f.OrbitCenter is Planet)
                {
                    continue;
                }

                var ra = Relation(attacker.Owner, f);
                var rt = Relation(target.Owner, f);
                var joins = pass == 1 ? ra == Treaty.Allied || rt == Treaty.Allied : ra is > 0 and < 4 || rt is > 0 and < 4;
                if (!joins)
                {
                    continue;
                }

                // Bug kept: a joiner whose own slot is free is left out.
                var s = Slot(Direction(f.X, f.Y, target.X, target.Y));
                if (!used[s])
                {
                    // The fix: the fleet comes in at its own free slot.
                    if (Fixed(Fixes.FleetsJoinBattles))
                    {
                        used[s] = true;
                        AddFleet(battle, f, _points[s], X87.ToInt(Direction(_points[s].X, _points[s].Y, target.X, target.Y)));
                    }

                    continue;
                }

                int best = 8, key = -8;
                for (var i = 0; i < 8; i++)
                {
                    if (!used[i])
                    {
                        var c = s + key;
                        if (c < 0)
                        {
                            c = best - s;
                        }

                        if (c < best)
                        {
                            best = i;
                            key = -i;
                        }
                    }
                }

                if (best == 8)
                {
                    break;
                }

                used[best] = true;
                AddFleet(battle, f, _points[best], X87.ToInt(Direction(_points[best].X, _points[best].Y, target.X, target.Y)));
            }
        }

        Battles.Insert(0, battle);
    }

    /// <summary>
    /// A fleet attacks a colony. Undefended, a fleet that can strike
    /// planets destroys it outright; defended, there is a battle.
    /// </summary>
    internal void StartPlanetBattle(Fleet attacker, Planet planet, ref StepEvents events)
    {
        // A mod may take the battle over (docs/modding.md): the fleet circles the planet instead.
        if (TakeOverBattle?.Invoke(this, attacker, planet) == true)
        {
            attacker.EnterOrbit(Random, planet);
            attacker.OrbitAngle = Direction(planet.X, planet.Y, attacker.X, attacker.Y);
            return;
        }

        var colony = planet.Colony!; // the callers have checked there is one
        var owner = colony.Owner;
        var star = Universe.Stars[colony.Star];
        var defended = colony.Structures.Any(s => s.Tech.Kind is 7 or 8 or 9 or 10)
            || star.Fleets.Any(f => f != attacker && !f.InCombat && f.OrbitCenter == planet);
        if (!defended)
        {
            if (attacker.Ships.Any(s => s.CanAttackPlanets))
            {
                var population = colony.CurrentPopulation;
                DestroyColony(colony, ref events);
                SetTreaty(attacker.Owner, owner, Treaty.War, Treaty.InForce, ref events);
                SetTreaty(owner, attacker.Owner, Treaty.War, Treaty.InForce, ref events);
                Incident(attacker.Owner, owner, IncidentKind.ColonyTaken, population);
            }

            attacker.EnterOrbit(Random, planet);
            attacker.OrbitAngle = Direction(planet.X, planet.Y, attacker.X, attacker.Y);
            return;
        }

        var battle = new Battle(colony.Star) { Id = NextBattleId++ };
        AddPlanet(battle, planet);
        Hold(attacker);

        int[] defenderSlots = [9, 0, 2, 8];
        int[] attackerSlots = [4, 6, 3, 7];
        int d = 0, a = 0;
        int Rel(Player side, Fleet f) => f.Owner == side ? Treaty.Allied : side.Treaties[f.Owner.Index].Level();
        int Face((int X, int Y) from, int toPoint) => X87.ToInt(Direction(from.X, from.Y, _points[toPoint].X, _points[toPoint].Y));

        void AddDefender(Fleet f)
        {
            var at = _points[defenderSlots[d++]];
            AddFleet(battle, f, at, Face(at, 5));
        }

        void AddAttacker(Fleet f, int facePoint)
        {
            var at = _points[attackerSlots[a++]];
            AddFleet(battle, f, at, Face(at, facePoint));
        }

        foreach (var f in star.Fleets.ToList())
        {
            if (d >= 4)
            {
                break;
            }

            if (f.OrbitCenter == planet && !f.InCombat && f.Owner == owner)
            {
                AddDefender(f);
            }
        }

        foreach (var f in star.Fleets.ToList())
        {
            if (d >= 4)
            {
                break;
            }

            if (f.OrbitCenter == planet && !f.InCombat && f.Owner != owner)
            {
                if (Rel(owner, f) == Treaty.Allied)
                {
                    AddDefender(f);
                }
                else if (Rel(attacker.Owner, f) == Treaty.Allied && a < 4)
                {
                    AddAttacker(f, 9);
                }
            }
        }

        foreach (var f in star.Fleets.ToList())
        {
            if (d >= 4 && a >= 4)
            {
                break;
            }

            if (!f.InCombat && f.OrbitCenter is null && Distance(f.X, f.Y, planet.X, planet.Y) <= JoinDistance)
            {
                if (Rel(owner, f) == Treaty.Allied && d < 4)
                {
                    AddDefender(f);
                }
                else if (Rel(attacker.Owner, f) == Treaty.Allied && a < 4)
                {
                    AddAttacker(f, 9);
                }
            }
        }

        foreach (var f in star.Fleets.ToList())
        {
            if (d >= 4 && a >= 4)
            {
                break;
            }

            if (!f.InCombat && f.OrbitCenter is null && Distance(f.X, f.Y, planet.X, planet.Y) <= JoinDistance)
            {
                var rd = Rel(owner, f);
                var ra = Rel(attacker.Owner, f);

                // The fix: a fleet on terms with the attacker attacks, one on terms with the defender defends.
                if (Fixed(Fixes.PlanetBattleSides))
                {
                    if (ra is > 0 and < 4 && a < 4)
                    {
                        AddAttacker(f, 5);
                    }
                    else if (rd is > 0 and < 4 && d < 4)
                    {
                        AddDefender(f);
                    }
                }

                // Bug kept: the second test reads the defender's relation.
                else if (rd is > 0 and < 4 || (ra > 0 && rd < 4))
                {
                    if (a < 4)
                    {
                        AddAttacker(f, 5);
                    }
                    else if (d < 4)
                    {
                        AddDefender(f);
                    }
                }
            }
        }

        var last = d == 0 && a == 0 ? _points[10] : _points[5];
        AddFleet(battle, attacker, last, 45);
        Battles.Insert(0, battle);
    }

    /// <summary>A fleet is held in a battle: not retreating, no boost.</summary>
    private void Hold(Fleet fleet)
    {
        fleet.InCombat = true;
        fleet.Retreating = false;
        fleet.BoostTicks = 0;
        fleet.UpdateStats(Tables.Tech);
    }

    private void AddFleet(Battle battle, Fleet fleet, (int X, int Y) point, int facing)
    {
        battle.Marker ??= (fleet.X, fleet.Y);
        Hold(fleet);
        var p = fleet.Owner.Index;
        battle.Commanders[p] ??= new Commander(p, fleet.Owner.IsAi);
        foreach (var ship in fleet.Ships)
        {
            ship.EnterBattle();
            var fighting = new BattleShip(ship, fleet, p) { Id = TakeCombatId() };
            battle.Ships[p].Insert(0, fighting);
            BuildShipFighters(battle, fighting);
        }

        if (!fleet.Owner.IsAi)
        {
            battle.Humans++;
        }

        Place(battle, p, point, facing);

        // The battle marker on the star map takes the first orbiting fleet's orbit, drawing a new one first.
        if (fleet.OrbitCenter is not null && !battle.MarkerOrbits)
        {
            // on the marker: an angle and a direction are drawn, then overwritten by the fleet's.
            Random.Next(3600);
            Random.Next(100);
            battle.MarkerOrbits = true;
        }
    }

    /// <summary>
    /// Places a side's new ships around a point by hull type, rows of up to
    /// seven, turned by the facing, each on a free cell.
    /// </summary>
    private void Place(Battle battle, int p, (int X, int Y) point, int facing)
    {
        var cx = point.X / Battle.CellSize;
        var cy = point.Y / Battle.CellSize;
        int index = 0, row = 0;
        int up = cy, down = cy;
        var firstRow = true;
        foreach (var type in _placementOrder)
        {
            foreach (var bs in battle.Ships[p])
            {
                if ((bs.X != 0 && bs.Y != 0) || bs.Ship.Type != type)
                {
                    continue;
                }

                if (firstRow && type != 2)
                {
                    firstRow = false;
                    row++;
                    index = 0;
                    up = down = cy;
                }

                var rowX = row * 5;
                var placed = false;
                while (!placed)
                {
                    if (index > 6)
                    {
                        row++;
                        rowX += 5;
                        index = 0;
                        up = down = cy;
                    }

                    var x = cx - index / 2 + rowX;
                    int y;
                    if (index % 2 == 0)
                    {
                        y = up;
                        up -= 5;
                    }
                    else
                    {
                        y = down;
                        down += 5;
                    }

                    if (facing >= 0)
                    {
                        var dx = (double)(x - cx);
                        var dy = (double)(y - cy);
                        var rx = X87.ToInt(dx * Tables.Cos[facing] - dy * Tables.Sin[facing] - -0.5);
                        var ry = X87.ToInt(dy * Tables.Cos[facing] + dx * Tables.Sin[facing] - -0.5);
                        x = rx + cx;
                        y = ry + cy;
                    }

                    if (battle.Free(x, y))
                    {
                        bs.SetPosition(x * Battle.CellSize, y * Battle.CellSize);
                        bs.Facing = facing;
                        bs.UpdateSpriteFrame();
                        battle.MarkFootprint(bs.X, bs.Y, (bs.Radius + 1) / 2, bs.Id);
                        placed = true;
                        index++;
                    }
                }
            }
        }
    }

    private void AddPlanet(Battle battle, Planet planet)
    {
        var colony = planet.Colony!; // only planet battles call this, at a colony
        var owner = colony.Owner;
        var p = owner.Index;
        planet.InCombat = true;
        if (!owner.IsAi)
        {
            battle.Humans++;
        }

        battle.Commanders[p] ??= new Commander(p, owner.IsAi);
        battle.Planet = planet;
        battle.PlanetObject = new CombatPlanet { Id = TakeCombatId() };

        // The planet's disc on the grid.
        for (int a = 370, x = 3230; a > 55; a -= 10, x += 10)
        {
            battle.MarkFootprint(x, X87.ToInt(a * 1.36 - -0.5) + 3040, 50, battle.PlanetObject.Id);
        }

        battle.Bands = Bands(colony);
        for (var i = 0; i <= battle.Bands; i++)
        {
            var block = new PlanetBlock(i) { Id = TakeCombatId() };
            PlaceBlock(block, planet);
            battle.Blocks.Insert(0, block);
        }

        foreach (var s in colony.Structures.ToList())
        {
            if (s.Tech.Kind == TechInfo.MineFieldKind)
            {
                LayMines(battle, s, p);
            }
            else if (s.Percent != 0.0 && _orbitalPoints.TryGetValue(s.Tech.Kind, out var at))
            {
                var size = (OrbitalSpriteRow(s) + 30) & 0xFF;
                var orbital = new Orbital(s, p) { Id = TakeCombatId(), Size = size, TrackLimit = s.SensorTech?.SensorTargets ?? 0 };
                orbital.SetPosition(at.X, at.Y);
                battle.MarkFootprint(at.X, at.Y, (size + 1) / 2, orbital.Id);
                s.ResetForBattle();
                battle.Orbitals[p].Insert(0, orbital);
                if (s.Tech.Kind == TechInfo.FighterBaseKind)
                {
                    BuildBaseFighters(battle, orbital);
                }
            }
        }
    }

    /// <summary>
    /// Where a block stands: its spot in a fixed layout round the
    /// planet in the field's far corner, shifted by half its picture's width
    /// and by its picture's height.
    /// </summary>
    private void PlaceBlock(PlanetBlock block, Planet planet)
    {
        var picture = (planet.Class == 4 ? 20100 : 20200) + block.Index;
        var (w, h) = Tables.CombatPictureSizes.GetValueOrDefault(picture);
        var (tx, ty) = _blockLayout[block.Index];
        var x = w * 0.5 + (tx + 3230);
        var y = ty * 1.7434467956212383 + h * 0.6 - -3090.0;
        block.SetPosition(x, y);
    }

    /// <summary>Each block's spot before the picture's shift (the table).</summary>
    private static readonly (int X, int Y)[] _blockLayout =
    [
        (166, 152), (220, 91), (124, 187), (178, 144), (155, 180),
        (211, 125), (188, 135), (280, 94), (124, 226), (226, 124),
    ];

    /// <summary>The last visible row of the orbital's first sprite frame, which sets its footprint.</summary>
    private int OrbitalSpriteRow(Structure s)
    {
        var n = s.Count;
        var id = s.Tech.Kind switch
        {
            8 => Math.Min((n + 339) * 50, 17450),
            7 => Math.Min((n + 349) * 50, 17950),
            9 => Math.Min((n + 359) * 50, 18450),
            5 => 19960,
            6 => 19910,
            _ => 0,
        };
        return Tables.CombatSpriteRows.GetValueOrDefault(id);
    }

    /// <summary>(improvements under way - 1) / 5, or -1 with none (or no colony).</summary>
    private static int Bands(Colony? colony)
    {
        var count = colony?.Improvements.Count(i => i is not null && i.Percent > 0.0) ?? 0;
        return count == 0 ? -1 : (count - 1) / 5;
    }
}
