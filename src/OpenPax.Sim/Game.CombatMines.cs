using OpenPax.Sim.Combat;
using OpenPax.Sim.Numerics;
using OpenPax.Sim.Players;

namespace OpenPax.Sim;

/// <summary>
/// Mines round a colony with a minefield (docs/sim/combat-step.md). They are
/// laid in every planet battle, also one resolved at once, where they only
/// cost draws. In a battle fought step by step they creep toward enemy
/// ships, blow up on touching one, and wear the minefield down as they are
/// lost.
/// </summary>
public sealed partial class Game
{
    private const int MineRingInner = 700;
    private const int MineRingOuter = 2000;
    private const int MineFieldCorner = 3600;
    private const int MineStopDistance = 240;
    private const int MinesPerUnit = 75;

    /// <summary>
    /// A minefield's mines: count times 75 by what
    /// is left of it. Each is laid at random in a ring 700 to 2000 from the
    /// planet's corner on free cells, drawing again until a point fits.
    /// </summary>
    private void LayMines(Battle battle, Structure field, int owner)
    {
        var n = (int)(field.Percent * (field.Count * MinesPerUnit) * 0.01);
        var level = Tables.IndexInKind(field.Tech) + 1;
        for (var m = 0; m < n; m++)
        {
            var mine = new Mine(owner, field, level) { Id = TakeCombatId() };
            int x = MineFieldCorner, y = MineFieldCorner;
            while (true)
            {
                var d = X87.ToInt(DistanceD(MineFieldCorner, MineFieldCorner, x, y));
                if (d > MineRingInner && d <= MineRingOuter)
                {
                    break;
                }

                x = Random.Next(3600);
                y = Random.Next(3600);

                // the mine's own footprint must be free; mines never mark it.
                if (battle.Taken(x, y, (Mine.Size + 1) / 2) != 0)
                {
                    (x, y) = (MineFieldCorner, MineFieldCorner);
                }
            }

            mine.SetPosition(x, y);
            (mine.HomeX, mine.HomeY) = (x, y);
            battle.Mines[owner].Insert(0, mine);
        }

        field.ResetForBattle();
        field.MinesDeployed = n;
    }

    /// <summary>
    /// Whether a side sees a player's mines: its own, a player
    /// it is not at war with, or with a ship whose minesweeper is of the
    /// mines' level or better. Quirk kept: the newest mine's level stands for
    /// the whole list.
    /// </summary>
    public bool MinesVisible(Battle battle, int owner, int viewer)
    {
        if (battle.Mines[owner].Count == 0 || owner == viewer || !AtWar(viewer, owner) || battle.Ships[viewer].Count == 0)
        {
            return true;
        }

        var level = battle.Mines[owner][0].Level;
        return battle.Ships[viewer].Any(s => MinesweeperLevel(s.Ship) >= level);
    }

    /// <summary>The level of the first minesweeper fitted, 0 for none; a minefield of a higher level stays unseen by the ship.</summary>
    [ModApi]
    public int MinesweeperLevel(Design design)
    {
        var sweeper = design.Specials.FirstOrDefault(c => c.Tech.Kind == TechInfo.MinesweeperKind);
        return sweeper is null ? 0 : Tables.IndexInKind(sweeper.Tech) + 1;
    }

    /// <summary>
    /// A computer ship under way stops when it sees an enemy mine near
    /// (the start of).
    /// </summary>
    private void MineStop(Battle battle, BattleShip ship)
    {
        if (battle.Commanders[ship.Owner] is not { Computer: true, Counter: >= 7 } || ship.State == BattleShip.Idle)
        {
            return;
        }

        for (var q = 0; q < Battle.Players; q++)
        {
            if (q == ship.Owner || !AtWar(ship.Owner, q) || !MinesVisible(battle, q, ship.Owner))
            {
                continue;
            }

            if (battle.Mines[q].Any(m => !m.Dead && X87.ToInt(DistanceD(ship.PosX, ship.PosY, m.PosX, m.PosY) - -0.5) < MineStopDistance))
            {
                ship.State = BattleShip.Idle;
            }
        }
    }

    /// <summary>How much a mine wants a ship: the nearer the better.</summary>
    private static int MineScore(Mine mine, BattleObject o) => 10000 - X87.ToInt(DistanceD(mine.PosX, mine.PosY, o.PosX, o.PosY) - -0.5);

    /// <summary>
    /// A mine looks for ships: it lets go of one dead or out of
    /// range, and takes the nearest enemy ship in range. Quirk kept: a nearer
    /// ship found ends the look at that player's ships.
    /// </summary>
    private void ScanMine(Battle battle, Mine mine)
    {
        if (mine.Dead)
        {
            return;
        }

        if (mine.Target is { } held && (held.Dead || DistanceD(mine.PosX, mine.PosY, held.PosX, held.PosY) > mine.SensorRange))
        {
            mine.Target = null;
        }

        for (var q = 0; q < Battle.Players; q++)
        {
            if (!AtWar(mine.Owner, q))
            {
                continue;
            }

            foreach (var ship in battle.Ships[q])
            {
                if (ship.Dead || DistanceD(mine.PosX, mine.PosY, ship.PosX, ship.PosY) >= mine.SensorRange)
                {
                    continue;
                }

                if (mine.Target is null)
                {
                    mine.Target = ship;
                    continue;
                }

                if (MineScore(mine, ship) > MineScore(mine, mine.Target))
                {
                    mine.Target = ship;
                    break;
                }
            }
        }
    }

    /// <summary>A mine creeps toward its ship, or home, over everything.</summary>
    private void MoveMine(Battle battle, Mine mine)
    {
        if (mine.Target is { Dead: true })
        {
            mine.Target = null;
            ScanMine(battle, mine);
        }

        var (gx, gy) = mine.Target is { } t ? (t.X, t.Y) : (mine.HomeX, mine.HomeY);
        if (mine.X == gx && mine.Y == gy)
        {
            return;
        }

        mine.Speed = Mine.TopSpeed;
        mine.Facing = X87.ToInt(DirectionD(mine.PosX, mine.PosY, gx, gy));
        if (DistanceD(mine.PosX, mine.PosY, gx, gy) < mine.Speed)
        {
            mine.SetPosition(gx, gy);
            mine.Speed = 0.0;
            return;
        }

        var next = Along(mine, mine.Facing, mine.Speed);
        mine.SetPosition(next.X, next.Y);
    }

    /// <summary>
    /// Whether a point defense shot reached a mine: within its
    /// size, with no roll and a flash. Quirk kept: one round lands.
    /// </summary>
    private int MineHitTest(Battle battle, Mine mine, Shot shot)
    {
        if (DistanceD(mine.PosX, mine.PosY, shot.X, shot.Y) >= Mine.Size)
        {
            return 0;
        }

        StrikeFlash(battle, mine, shot);
        return 1;
    }

    /// <summary>
    /// Shots that reached a mine land: damage times rounds off
    /// its hit points; destroyed, the shooter is credited 3. Quirk kept:
    /// every shot marks the last damaging hit, even one doing nothing.
    /// </summary>
    private void ResolveMineHits(Battle battle, Mine mine, ref StepEvents events)
    {
        if (mine.Dead)
        {
            return;
        }

        foreach (var h in mine.Incoming)
        {
            if (!mine.Dead)
            {
                mine.HitPoints -= h.RoundDamage() * h.Rounds;
                if (mine.HitPoints < 1)
                {
                    MineExplodes(battle, mine, ref events);
                    if (battle.Commanders[h.Owner] is { } shooter)
                    {
                        shooter.Kills += 3;
                    }
                }

                battle.LastHit = battle.Steps;
                h.Speed = 0.0;
            }

            ShotDies(battle, h, explode: false);
        }

        mine.Incoming.Clear();
    }

    /// <summary>
    /// A mine is lost: the minefield loses a seventy-fifth of
    /// its tech's hit points and a mine out, and goes once none is out. In a
    /// field above the first level a resting mine moves up to fill the gap.
    /// A minefield already destroyed by the loss is not touched again (the
    /// original writes to it after freeing it).
    /// </summary>
    private void MineExplodes(Battle battle, Mine mine, ref StepEvents events)
    {
        mine.Target = null;
        mine.Dead = true;
        AddEffect(battle.ShipEffects, 3, mine.X, mine.Y);
        var field = mine.Field;
        var colony = field.Colony;
        if (colony.Structures.Contains(field) && HitStructure(field, field.Tech.HitPoints / MinesPerUnit, ref events))
        {
            field.MinesDeployed--;
        }

        battle.Mines[mine.Owner].Remove(mine);
        if (mine.Level > 1 && battle.Mines.FirstOrDefault(l => l.Count > 0) is { } list
            && list.FirstOrDefault(m => m.Speed == 0.0) is { } resting)
        {
            (resting.HomeX, resting.HomeY) = (mine.HomeX, mine.HomeY);
        }

        if (field.MinesDeployed < 1 && colony.Structures.Contains(field))
        {
            while (colony.Structures.Contains(field))
            {
                RemoveStructureUnit(field);
            }
        }
    }

    /// <summary>
    /// Enemy mines a ship touches blow up on it (the end of,
    /// ): each takes its damage straight off the structure, with
    /// no shield, credit or incident.
    /// </summary>
    private void MineContact(Battle battle, BattleShip ship, ref StepEvents events)
    {
        while (!ship.Dead)
        {
            Mine? touching = null;
            for (var q = 0; q < Battle.Players && touching is null; q++)
            {
                if (AtWar(ship.Owner, q))
                {
                    touching = battle.Mines[q].FirstOrDefault(m => DistanceD(ship.PosX, ship.PosY, m.PosX, m.PosY) <= ship.Radius * 0.5);
                }
            }

            if (touching is null)
            {
                return;
            }

            ship.Tracked.Remove(touching);
            MineExplodes(battle, touching, ref events);
            ship.Ship.Structure -= touching.Damage;
            if (ship.Ship.Structure <= 0)
            {
                TacticalShipDies(battle, ship);
            }
        }
    }

    /// <summary>A mine's post-step: a dead ship let go.</summary>
    private static void PostStepMine(Mine mine)
    {
        if (mine.Target is { Dead: true })
        {
            mine.Target = null;
        }
    }

    /// <summary>Every mine on the field goes with the colony, with no further loss to anything.</summary>
    private static void PurgeMines(Battle battle)
    {
        foreach (var list in battle.Mines)
        {
            foreach (var mine in list)
            {
                mine.Dead = true;
            }

            list.Clear();
        }
    }
}
