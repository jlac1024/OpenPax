using OpenPax.Sim.Combat;
using OpenPax.Sim.Numerics;
using OpenPax.Sim.Players;

namespace OpenPax.Sim;

/// <summary>
/// Fighters in a battle fought step by step (docs/sim/combat-step.md). Each
/// launch or new target draws one R(5) for the first point of the circle
/// it flies round its target; every later point draws again.
/// </summary>
public sealed partial class Game
{
    private const double DockDistance = 20.0;
    private const double FighterReach = 300.0;
    private const double FighterHitRadius = 20.0;
    private const double AutoLaunchRange = 5000.0;
    private const int FighterPower = 50;
    private const int StraightOut = 50;
    private const int OnCarrierHeading = 25;
    private const int FieldMargin = 20;

    /// <summary>The circle a fighter flies round its target, 225 out in steps of 22.5 degrees (the table).</summary>
    private static readonly (int X, int Y)[] _fighterCircle =
    [
        (225, 0), (195, -113), (159, -159), (113, -195), (0, -225), (-113, -195), (-159, -159), (-195, -113),
        (-225, 0), (-195, 113), (-159, 159), (-113, 195), (0, 225), (113, 195), (159, 159), (195, 113),
    ];

    /// <summary>Where each craft of a flight fires from (the table).</summary>
    private static readonly (int X, int Y)[] _craftOffsets = [(-10, 0), (13, -13), (13, 13)];

    /// <summary>
    /// A carrier ship's fighters: as many flights as each bay
    /// has working, docked. Quirk kept: they are made before the ship is
    /// placed, so they wait at (0, 0) until launched.
    /// </summary>
    private void BuildShipFighters(Battle battle, BattleShip ship)
    {
        foreach (var bay in ship.Ship.Fighters)
        {
            for (var i = 0; i < bay.Operational; i++)
            {
                AddFighter(battle, new Fighter(ship.Owner, bay.Tech, ship, bay, Tables.Tech));
            }
        }
    }

    /// <summary>
    /// A fighter base's fighters: count times two by what is
    /// left of it, plus one, of each fighter fitted, taken in turn.
    /// </summary>
    private void BuildBaseFighters(Battle battle, Orbital orbital)
    {
        var s = orbital.Structure;
        var n = (int)(s.Count * 2 * s.Percent * 0.01);
        for (var r = 0; r <= n; r++)
        {
            foreach (var tech in s.FighterTechs)
            {
                if (tech is not null)
                {
                    var fighter = new Fighter(orbital.Owner, tech, orbital, null, Tables.Tech);
                    fighter.SetPosition(orbital.X, orbital.Y);
                    AddFighter(battle, fighter);
                }
            }
        }
    }

    private void AddFighter(Battle battle, Fighter fighter)
    {
        fighter.Id = TakeCombatId();
        battle.Fighters[fighter.Owner].Insert(0, fighter);
    }

    /// <summary>
    /// A fighter decides: one coming home docks within 20 of its
    /// carrier, or with the carrier gone becomes an orphan; one whose target
    /// is dead takes the nearest enemy, else goes home; an orphan with no
    /// enemy left is lost.
    /// </summary>
    private void FighterThink(Battle battle, Fighter f)
    {
        while (true)
        {
            switch (f.State)
            {
                case Fighter.Returning:
                    if (f.Carrier is { Dead: false } carrier)
                    {
                        if (DistanceD(carrier.PosX, carrier.PosY, f.PosX, f.PosY) < DockDistance)
                        {
                            Dock(f);
                        }

                        return;
                    }

                    f.State = Fighter.Orphan;
                    f.Carrier = null;
                    continue;
                case Fighter.Attacking or Fighter.Escorting:
                    if (f.Link is { Dead: false })
                    {
                        return;
                    }

                    f.Link = null;
                    if (FighterRetarget(battle, f))
                    {
                        return;
                    }

                    (f.WaypointX, f.WaypointY) = (0, 0);
                    f.State = Fighter.Returning;
                    continue;
                case Fighter.Orphan:
                    if (f.Link is { Dead: false } || FighterRetarget(battle, f))
                    {
                        return;
                    }

                    AddEffect(battle.ShipEffects, 6, f.PosX, f.PosY);
                    FighterDies(battle, f);
                    return;
                default:
                    return;
            }
        }
    }

    /// <summary>Docks: the craft in front repaired, the target let go. The craft lost stay lost; it stays where it is.</summary>
    private static void Dock(Fighter f)
    {
        f.Docked = true;
        f.HitPoints = f.MaxHitPoints;
        f.Link = null;
        f.State = Fighter.DockedState;
        (f.WaypointX, f.WaypointY) = (0, 0);
    }

    /// <summary>The nearest enemy ship, orbital or fighter (docked ones too) anywhere on the field, attacked.</summary>
    private bool FighterRetarget(Battle battle, Fighter f)
    {
        BattleObject? best = null;
        var bestD = 0.0;
        void Consider(BattleObject o)
        {
            if (o.Dead)
            {
                return;
            }

            var d = DistanceD(f.PosX, f.PosY, o.PosX, o.PosY);
            if (best is null || d < bestD)
            {
                (best, bestD) = (o, d);
            }
        }

        for (var q = 0; q < Battle.Players; q++)
        {
            if (!AtWar(f.Owner, q))
            {
                continue;
            }

            battle.Ships[q].ForEach(Consider);
            battle.Orbitals[q].ForEach(Consider);
            battle.Fighters[q].ForEach(Consider);
        }

        f.Link = null;
        if (best is null)
        {
            return false;
        }

        FighterAttack(battle, f, best);
        return true;
    }

    /// <summary>
    /// A fighter goes for a target: a docked one leaves from its
    /// carrier, from a ship flying straight out first.
    /// </summary>
    private void FighterAttack(Battle battle, Fighter f, BattleObject target)
    {
        (f.WaypointX, f.WaypointY) = (0, 0);
        f.Link = target;
        f.Straight = 0;
        if (f.Carrier is { Dead: false } carrier && f.Docked)
        {
            f.SetPosition(carrier.X, carrier.Y);
            if (carrier is BattleShip)
            {
                f.Straight = StraightOut;
            }
        }

        f.State = Fighter.Attacking;
        f.Docked = false;
        FighterThink(battle, f);
        FighterTurn(f);
    }

    /// <summary>A fighter guards a friend, aiming for itself at once.</summary>
    private void FighterEscort(Battle battle, Fighter f, BattleObject friend)
    {
        (f.WaypointX, f.WaypointY) = (0, 0);
        f.Link = friend;
        if (f.Docked && f.Carrier is { Dead: false } carrier)
        {
            f.SetPosition(carrier.X, carrier.Y);
        }

        f.State = Fighter.Escorting;
        f.Docked = false;
        f.Straight = 0;
        FighterThink(battle, f);
        FighterTurn(f);
    }

    /// <summary>
    /// The next point on the circle round the target, taken when
    /// it has none, has reached it, or the target has moved more than 300
    /// from it. One time in five, or near the field's edge, it crosses to
    /// the far side and goes round the other way. Quirk kept: the index
    /// wraps by 15, so the first point is skipped on the way round.
    /// </summary>
    private void FighterWaypoint(Fighter f)
    {
        if (f.Link is not { } target)
        {
            return;
        }

        var none = f.WaypointX == 0 && f.WaypointY == 0;
        var reached = f.WaypointX == f.X && f.WaypointY == f.Y;
        var drifted = f.State is Fighter.Attacking or Fighter.Escorting or Fighter.Orphan
            && Distance(f.WaypointX, f.WaypointY, target.X, target.Y) > FighterReach;
        if (!none && !reached && !drifted)
        {
            return;
        }

        var (dx, dy) = _fighterCircle[f.WaypointIndex];
        (f.WaypointX, f.WaypointY) = (target.X + dx, target.Y + dy);
        f.WaypointIndex += f.WaypointStep;
        var edge = f.X < FieldMargin || f.X > FieldSize - FieldMargin || f.Y < FieldMargin || f.Y > FieldSize - FieldMargin;
        if (Random.Next(5) == 0 || edge)
        {
            f.WaypointIndex += 8;
            f.WaypointStep = -f.WaypointStep;
        }

        if (f.WaypointIndex > 15)
        {
            f.WaypointIndex -= 15;
        }

        if (f.WaypointIndex < 0)
        {
            f.WaypointIndex += 15;
        }
    }

    /// <summary>Where a fighter heads: its carrier coming home, else its circle's point; null for an orphan with nothing to chase.</summary>
    private (double X, double Y)? FighterGoal(Fighter f)
    {
        if (f.State == Fighter.Returning)
        {
            return f.Carrier is { } carrier ? (carrier.X, carrier.Y) : null;
        }

        if (f.State == Fighter.Orphan && f.Link is not { Dead: false })
        {
            return null;
        }

        FighterWaypoint(f);
        return (f.WaypointX, f.WaypointY);
    }

    /// <summary>
    /// A fighter's heading: it snaps to its goal, except after
    /// leaving a ship, when it flies on straight, then on the ship's heading.
    /// </summary>
    private void FighterTurn(Fighter f)
    {
        if (FighterGoal(f) is not { } g)
        {
            return;
        }

        if (f.Carrier is not BattleShip { Dead: false })
        {
            f.Straight = 0;
        }

        if (f.Straight == 0)
        {
            f.Facing = X87.ToInt(DirectionD(f.PosX, f.PosY, g.X, g.Y));
        }
        else if (f.Straight <= OnCarrierHeading)
        {
            f.Facing = f.Carrier!.Facing; // a ship carrier, tested above
            f.Straight--;
        }
        else
        {
            f.Straight--;
        }

        f.WantedHeading = f.Facing;
    }

    /// <summary>A fighter's move: over everything, at full speed, onto its goal when within a step.</summary>
    private void MoveFighter(Battle battle, Fighter f)
    {
        FighterThink(battle, f);
        if (f.Docked || f.State == Fighter.DockedState)
        {
            return;
        }

        FighterTurn(f);
        f.Speed = f.TopSpeed;
        if (FighterGoal(f) is not { } g)
        {
            return;
        }

        if (DistanceD(g.X, g.Y, f.PosX, f.PosY) < f.Speed)
        {
            f.SetPosition(g.X, g.Y);
            if (f.WaypointX == 0 && f.WaypointY == 0)
            {
                f.Speed = 0.0;
            }

            return;
        }

        var next = Along(f, f.Facing, f.Speed);
        f.SetPosition(next.X, next.Y);
    }

    /// <summary>
    /// A fighter fires at its target, or when guarding at the
    /// enemy nearest its friend, from within 300: one shot per craft at half
    /// power. A weapon counts down its reload, two a phase, only while there
    /// is something to shoot at. Quirk kept: an orphan never fires.
    /// </summary>
    private void FireFighter(Battle battle, Fighter f)
    {
        FighterThink(battle, f);
        BattleObject? target = null;
        if (f.State == Fighter.Attacking)
        {
            target = f.Link;
        }
        else if (f.State == Fighter.Escorting && f.Link is { } friend)
        {
            var bestD = 0.0;
            for (var q = 0; q < Battle.Players; q++)
            {
                if (!AtWar(f.Owner, q))
                {
                    continue;
                }

                foreach (var o in battle.Ships[q].Cast<BattleObject>().Concat(battle.Fighters[q]))
                {
                    var d = DistanceD(friend.PosX, friend.PosY, o.PosX, o.PosY);
                    if (target is null || d < bestD)
                    {
                        (target, bestD) = (o, d);
                    }
                }
            }
        }

        if (target is null)
        {
            return;
        }

        if (f.Reload1 < 1)
        {
            if (DistanceD(f.PosX, f.PosY, target.PosX, target.PosY) < FighterReach)
            {
                FighterVolley(battle, f, f.Weapon1!, target); // a weapon slot it lacks never comes off 9999
                f.Reload1 = f.Weapon1?.Reload ?? Fighter.NoWeapon;
            }
        }
        else if (f.Weapon1 is not null)
        {
            f.Reload1 -= 2;
        }

        if (f.Reload2 < 1)
        {
            if (DistanceD(f.PosX, f.PosY, target.PosX, target.PosY) < FighterReach)
            {
                FighterVolley(battle, f, f.Weapon2!, target); // as above
                f.Reload2 = f.Weapon2?.Reload ?? Fighter.NoWeapon;
            }
        }
        else if (f.Weapon2 is not null)
        {
            f.Reload2 -= 2;
        }
    }

    /// <summary>One shot from each craft, a round each at the fighter's accuracy and half power.</summary>
    private void FighterVolley(Battle battle, Fighter f, TechInfo weapon, BattleObject target)
    {
        for (var i = 0; i < f.Craft; i++)
        {
            var (dx, dy) = _craftOffsets[i];
            Launch(battle, NewShot(f.Owner, weapon, false, f.X + dx, f.Y + dy, target, 1, (short)f.Tech.FighterAccuracy, FighterPower));
        }
    }

    /// <summary>
    /// Whether a shot reached a fighter: within 20, a roll of
    /// 100 a round. Quirks kept: all missed means not yet reached (it may
    /// roll again), and however many hit, one round lands.
    /// </summary>
    private int FighterHitTest(Fighter f, Shot shot)
    {
        if (DistanceD(f.PosX, f.PosY, shot.X, shot.Y) >= FighterHitRadius)
        {
            return 0;
        }

        var chance = shot.Tracking
            ? (sbyte)shot.Tech.WeaponFlag - f.Tech.FighterMissileDefense + 100
            : (short)(shot.Accuracy + shot.Tech.WeaponAccuracy) - f.Tech.FighterDefense + 50;
        var hit = false;
        for (var i = 0; i < shot.Rounds; i++)
        {
            hit |= Random.Next(100) < chance;
        }

        return hit ? 1 : 0;
    }

    /// <summary>
    /// Shots that reached a fighter land, each whole on the
    /// craft in front; at most one craft is lost a shot. Damage taken while
    /// the carrier ship lives wears its fighter bay.
    /// </summary>
    private void ResolveFighterHits(Battle battle, Fighter f)
    {
        if (f.Dead)
        {
            return;
        }

        foreach (var h in f.Incoming)
        {
            if (!f.Dead)
            {
                var x = h.RoundDamage() * h.Rounds;
                if (x > 0)
                {
                    battle.LastHit = battle.Steps;
                    DamageBay(f, Math.Min(x, f.HitPoints));
                    f.HitPoints -= x;
                    if (f.HitPoints < 1)
                    {
                        if (battle.Commanders[h.Owner] is { } shooter)
                        {
                            shooter.Kills += f.MaxHitPoints / 10;
                        }

                        AddEffect(battle.ShipEffects, 6, f.PosX, f.PosY);
                        f.Craft--;
                        f.HitPoints = f.MaxHitPoints;
                    }

                    if (f.Craft == 0)
                    {
                        FighterDies(battle, f);
                    }
                }

                h.Speed = 0.0;
            }

            ShotDies(battle, h, explode: false);
        }

        f.Incoming.Clear();
    }

    /// <summary>The carrier ship's bay takes the damage, up to what it can hold.</summary>
    private static void DamageBay(Fighter f, int amount)
    {
        if (f.Carrier is not BattleShip { Dead: false } || f.Bay is not { } bay)
        {
            return;
        }

        var room = bay.Count * bay.Tech.HitPoints - bay.Damage;
        if (room != 0)
        {
            bay.Damage = Math.Max(0, bay.Damage + Math.Min(amount, room));
        }
    }

    /// <summary>A fighter is lost.</summary>
    private static void FighterDies(Battle battle, Fighter f)
    {
        if (f.Dead)
        {
            return;
        }

        f.Dead = true;
        battle.Fighters[f.Owner].Remove(f);
    }

    /// <summary>A fighter's post-step: it heads home when its ship starts to leave, and decides again.</summary>
    private void PostStepFighter(Battle battle, Fighter f)
    {
        if (f.Carrier is { } carrier)
        {
            if (carrier.Dead)
            {
                f.Carrier = null;
            }
            else if (carrier is BattleShip { State: BattleShip.Leaving } && !f.Docked)
            {
                f.State = Fighter.Returning;
            }
        }

        FighterThink(battle, f);
    }

    /// <summary>
    /// The carrier's fighter it can best spare (shared,
    /// ): the healthiest, those already on
    /// this job counting a little less, those on it for this object not at all.
    /// </summary>
    private static Fighter? SpareFighter(Battle battle, BattleObject carrier, int owner, int job, BattleObject target)
    {
        Fighter? pick = null;
        var best = 0;
        foreach (var f in battle.Fighters[owner])
        {
            if (f.Carrier != carrier || (f.State == job && f.Link == target))
            {
                continue;
            }

            var v = f.HitPoints * 100 / f.MaxHitPoints;
            if (v > best)
            {
                pick = f;
                best = f.State == job ? v - 20 : v;
                if (best == 100)
                {
                    break;
                }
            }
        }

        return pick;
    }

    private static List<Fighter> DockedFighters(Battle battle, BattleObject carrier, int owner) =>
        battle.Fighters[owner].Where(f => f.Carrier == carrier && f.Docked).ToList();

    /// <summary>A ship launches at an enemy: two docked fighters, one out each side, else one flying fighter turned onto it.</summary>
    private void LaunchFromShip(Battle battle, BattleShip ship, BattleObject target)
    {
        var docked = DockedFighters(battle, ship, ship.Owner);
        var a = docked.ElementAtOrDefault(0) ?? SpareFighter(battle, ship, ship.Owner, Fighter.Attacking, target);
        var b = docked.ElementAtOrDefault(1);
        if (a is not null)
        {
            FighterAttack(battle, a, target);
            a.Facing = WrapDegrees(ship.Facing + 45);
        }

        if (b is not null)
        {
            FighterAttack(battle, b, target);
            b.Facing = WrapDegrees(ship.Facing - 45);
        }
    }

    /// <summary>A ship or base sends one fighter to guard a friend.</summary>
    private void GuardWithFighter(Battle battle, BattleObject carrier, int owner, BattleObject friend)
    {
        var f = DockedFighters(battle, carrier, owner).FirstOrDefault() ?? SpareFighter(battle, carrier, owner, Fighter.Escorting, friend);
        if (f is not null)
        {
            FighterEscort(battle, f, friend);
        }
    }

    /// <summary>A base launches one fighter at an enemy, docked first.</summary>
    private void LaunchFromOrbital(Battle battle, Orbital orbital, BattleObject target)
    {
        var f = DockedFighters(battle, orbital, orbital.Owner).FirstOrDefault() ?? SpareFighter(battle, orbital, orbital.Owner, Fighter.Attacking, target);
        if (f is not null)
        {
            FighterAttack(battle, f, target);
        }
    }

    /// <summary>One fighter out comes home (a ship's skips those already coming).</summary>
    private static void RecallFighter(Battle battle, BattleObject carrier, int owner, bool skipReturning)
    {
        var f = battle.Fighters[owner].FirstOrDefault(f => f.Carrier == carrier && !f.Docked && !(skipReturning && f.State == Fighter.Returning));
        if (f is not null)
        {
            f.State = Fighter.Returning;
        }
    }

    /// <summary>
    /// A carrier gone: its docked fighters vanish
    /// from the battle (not lost, not counted), and those out fly on with
    /// no carrier.
    /// </summary>
    private static void CarrierGone(Battle battle, BattleObject carrier)
    {
        for (var p = 0; p < Battle.Players; p++)
        {
            battle.Fighters[p].RemoveAll(f => f.Carrier == carrier && f.Docked);
            foreach (var f in battle.Fighters[p])
            {
                if (f.Carrier == carrier)
                {
                    f.Carrier = null;
                }
            }
        }
    }
}
