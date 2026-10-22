using OpenPax.Sim.Combat;
using OpenPax.Sim.Numerics;
using OpenPax.Sim.Players;

namespace OpenPax.Sim;

/// <summary>
/// Sensors, firing, shots and hits in a battle fought step by step
/// (docs/sim/combat-step.md). Bombs at the planet, fighters and mines
/// come with their own pieces.
/// </summary>
public sealed partial class Game
{
    private const double FireRange = 1500.0;
    private const double PointDefenseRange = 350.0;
    private const double MissDistance = 500.0;

    /// <summary>
    /// Sensors look (every 10 sub-steps): the nearest enemy ship,
    /// else orbital, not yet followed, is added when within sensor range. At
    /// most one a look. Quirk kept: the test against the limit is "equal",
    /// so a sensor damaged below the count it follows keeps adding.
    /// </summary>
    internal void Scan(Battle battle, BattleShip ship)
    {
        if (ship.Dead || ship.Tracked.Count == ship.TargetsTracked)
        {
            return;
        }

        BattleObject? best = null;
        var bestScore = 0;
        void Consider(BattleObject o)
        {
            if (o.Dead || o == ship.Target || ship.Tracked.Contains(o))
            {
                return;
            }

            var score = 10000 - X87.ToInt(DistanceD(o.PosX, o.PosY, ship.PosX, ship.PosY) - -0.5);
            if (best is null || score > bestScore)
            {
                (best, bestScore) = (o, score);
            }
        }

        for (var q = 0; q < Battle.Players; q++)
        {
            if (q != ship.Owner && AtWar(ship.Owner, q))
            {
                battle.Ships[q].ForEach(Consider);
            }
        }

        for (var q = 0; q < Battle.Players; q++)
        {
            if (q != ship.Owner && AtWar(ship.Owner, q))
            {
                battle.Orbitals[q].ForEach(Consider);
            }
        }

        if (best is { } found && DistanceD(found.PosX, found.PosY, ship.PosX, ship.PosY) <= ship.SensorRange)
        {
            ship.Tracked.Insert(0, found);
        }
    }

    /// <summary>
    /// Which firing arc an object lies in: bow within 30 degrees
    /// of the heading, port to 180 on the one side, starboard on the other.
    /// Point defense fires all round.
    /// </summary>
    private static int Arc(BattleShip ship, BattleObject o)
    {
        var rel = Direction(ship.X, ship.Y, o.X, o.Y) - ship.Facing;
        while (rel < 0.0)
        {
            rel -= -360.0;
        }

        if (rel > 30.0 && rel < 330.0)
        {
            return rel <= 180.0 ? 1 : 2;
        }

        return 0;
    }

    /// <summary>A weapon's reach: the last band with damage, 300, 500, 750 or 1500.</summary>
    private static int WeaponRange(TechInfo tech)
    {
        var d = tech.Damage;
        return d[3] > 0 ? 1500 : d[2] > 0 ? 750 : d[1] > 0 ? 500 : d[0] > 0 ? 300 : 0;
    }

    /// <summary>
    /// How much point defense wants an object: nearer is more,
    /// a missile coming for this ship or a mine close by most, then a
    /// fighter after this ship; a missile already missed not at all; less
    /// for each shot already aimed at it.
    /// </summary>
    private static int PointDefenseScore(BattleShip ship, BattleObject o)
    {
        var d = X87.ToInt(DistanceD(o.PosX, o.PosY, ship.PosX, ship.PosY) - -0.5);
        if (d > 370)
        {
            return 0;
        }

        var score = 870 - d;
        switch (o)
        {
            case Shot { Missed: true }:
                return 0;
            case Shot shot when shot.Target == ship:
                score = 970 - d;
                break;
            case Mine when d < ship.Radius * 2:
                score = 970 - d;
                break;
            case Fighter f when f.Link == ship:
                score = 920 - d;
                break;
        }

        return score - o.AimedAt * 75;
    }

    /// <summary>
    /// A ship's weapons fire (every 5 sub-steps). Each arc takes
    /// the ordered target when it lies in that arc, else the nearest followed
    /// enemy in it within 1500; point defense takes the missile it wants most,
    /// else the nearest followed enemy within 350. Each ready weapon in reach
    /// of its arc's target fires; every weapon's reload then counts down.
    /// No draws.
    /// </summary>
    internal void FireShip(Battle battle, BattleShip ship)
    {
        if (ship.Dead)
        {
            return;
        }

        var ready = new bool[4];
        foreach (var w in ship.Ship.Weapons)
        {
            if (w.Mode is >= 0 and < 4 && w.Ready)
            {
                ready[w.Mode] = true;
            }
        }

        // the planet's highest standing block, near enough
        // and not the ship's own. Quirk kept: no war test; the war comes when
        // the bomb lands.
        BattleObject? bomb = null;
        if (battle.PlanetObject is not null && battle.Blocks.Count > 0
            && ship.Ship.Weapons.Any(w => w.Tech.WeaponType != 0 && w.Ready)
            && battle.Blocks.FirstOrDefault(b => !b.Dead) is { } block
            && DistanceD(block.PosX, block.PosY, ship.PosX, ship.PosY) < FireRange && PlanetOwner(battle) != ship.Owner)
        {
            bomb = block;
        }

        var targets = new BattleObject?[4];
        if (ready[3])
        {
            var bestScore = 1;
            for (var q = 0; q < Battle.Players; q++)
            {
                if (q == ship.Owner || !AtWar(ship.Owner, q))
                {
                    continue;
                }

                // Fighters, then missiles, then mines when the ship's side sees them.
                IEnumerable<BattleObject> wanted = battle.Fighters[q].Cast<BattleObject>().Concat(battle.Missiles[q]);
                if (MinesVisible(battle, q, ship.Owner))
                {
                    wanted = wanted.Concat(battle.Mines[q]);
                }

                foreach (var o in wanted)
                {
                    if (!o.Dead && PointDefenseScore(ship, o) is var score && score > bestScore)
                    {
                        (targets[3], bestScore) = (o, score);
                    }
                }
            }
        }

        if (ship.Target is { } ordered && ordered is not PlanetBlock)
        {
            var a = Arc(ship, ordered);
            if (ready[a] && targets[a] is null && DistanceD(ordered.PosX, ordered.PosY, ship.PosX, ship.PosY) <= FireRange)
            {
                targets[a] = ordered;
            }
        }

        for (var k = 0; k < 4; k++)
        {
            if (!ready[k] || targets[k] is not null)
            {
                continue;
            }

            var reach = k == 3 ? PointDefenseRange : FireRange;
            foreach (var o in ship.Tracked)
            {
                var a = k == 3 ? 3 : Arc(ship, o);
                if (a != k || o.Dead)
                {
                    continue;
                }

                var d = DistanceD(o.PosX, o.PosY, ship.PosX, ship.PosY);
                if (d <= reach && (targets[k] is not { } held || NearerThan(ship, o, held)))
                {
                    targets[k] = o;
                }
            }
        }

        foreach (var w in ship.Ship.Weapons)
        {
            if (w.Tech.WeaponType != 1 && w.Ready && w.Mode is >= 0 and < 4 && targets[w.Mode] is { } t
                && DistanceD(t.PosX, t.PosY, ship.PosX, ship.PosY) <= WeaponRange(w.Tech))
            {
                FireWeapon(battle, ship, w, t);
            }

            // Ready again: a volley not finished leaves no reload, so an area
            // weapon can fire at a ship and at the planet in the same phase.
            if (w.Tech.WeaponType != 0 && w.Ready && bomb is not null
                && DistanceD(bomb.PosX, bomb.PosY, ship.PosX, ship.PosY) <= WeaponRange(w.Tech))
            {
                FireWeapon(battle, ship, w, bomb);
            }

            if (w.ReloadLeft != 0)
            {
                w.ReloadLeft--;
            }
        }

        // Auto launch: the first followed enemy within 5000; with none it tries again next phase.
        if (ship.AutoLaunch && ship.State != BattleShip.Leaving)
        {
            if (ship.LaunchCountdown == 0)
            {
                if (ship.Tracked.FirstOrDefault(o => DistanceD(ship.PosX, ship.PosY, o.PosX, o.PosY) < AutoLaunchRange) is { } enemy)
                {
                    LaunchFromShip(battle, ship, enemy);
                    ship.LaunchCountdown = 5;
                }
            }
            else
            {
                ship.LaunchCountdown--;
            }
        }
    }

    /// <summary>Strictly nearer than the target held, by whole distance.</summary>
    private static bool NearerThan(BattleShip ship, BattleObject o, BattleObject held)
    {
        var a = 10000 - X87.ToInt(DistanceD(o.PosX, o.PosY, ship.PosX, ship.PosY) - -0.5);
        var b = 10000 - X87.ToInt(DistanceD(held.PosX, held.PosY, ship.PosX, ship.PosY) - -0.5);
        return a > b;
    }

    /// <summary>
    /// One weapon stack fires: a shot of up to size / 14 rounds
    /// (3 for point defense) from its volley, at its condition. At the
    /// planet the whole volley goes as one round of that much more power.
    /// </summary>
    private void FireWeapon(Battle battle, BattleShip ship, Component weapon, BattleObject target)
    {
        var n = weapon.Mode == 3 ? 3 : ship.Radius / 14;
        var fired = weapon.TakeVolley(n);
        if (fired == 0)
        {
            return;
        }

        var a = ship.Ship.CombatAttack();
        var shot = target is PlanetBlock or CombatPlanet
            ? NewShot(ship.Owner, weapon.Tech, false, ship.PosX, ship.PosY, target, 1, a * (a >= 0 ? fired : 1), weapon.Condition * fired)
            : NewShot(ship.Owner, weapon.Tech, weapon.Mode == 3, ship.PosX, ship.PosY, target, fired, a, weapon.Condition);
        Launch(battle, shot);
    }

    /// <summary>A new shot goes on its side's list.</summary>
    private static void Launch(Battle battle, Shot shot)
    {
        (shot.Tracking ? battle.Missiles : battle.Shots)[shot.Owner].Insert(0, shot);
        battle.LiveShots++;
        battle.LastShot = battle.Steps;
    }

    /// <summary>
    /// A new shot at the shooter's position: a missile starts
    /// slow and homes on its target; anything else flies at full speed on a
    /// heading aimed ahead of a moving target.
    /// </summary>
    private Shot NewShot(int owner, TechInfo tech, bool pointDefense, double x, double y, BattleObject target, int rounds, int accuracy, int power)
    {
        var shot = new Shot(owner, tech, pointDefense) { Id = TakeCombatId(), Target = target, Rounds = rounds, Accuracy = accuracy, Power = power };
        shot.SetPosition(x, y);
        if (shot.Tracking)
        {
            shot.Speed = Shot.Acceleration;
            shot.Goal = BattleGoal.Following(target);
            shot.Facing = X87.ToInt(DirectionD(shot.PosX, shot.PosY, target.PosX, target.PosY));
        }
        else
        {
            shot.Speed = shot.TopSpeed;
            LeadAim(shot, target);
        }

        // the spread across the line of flight, in steps of 22.5 degrees.
        var h = Galaxy.Orbit.Wrap(shot.Facing - -101.25);
        shot.SpreadGroup = X87.ToInt((h >= 180.0 ? h - 180.0 : h) * (1.0 / 22.5));

        target.AimedAt++;
        return shot;
    }

    /// <summary>The fix for fast shots: whether the shot's next step passes within a radius of a point.</summary>
    private bool PassesWithin(Shot shot, double x, double y, double radius)
    {
        var (ax, ay) = (shot.PosX, shot.PosY);
        var (bx, by) = Along(shot, shot.Facing, shot.Speed);
        var (dx, dy) = (bx - ax, by - ay);
        var length = (dx * dx) + (dy * dy);
        var t = length == 0 ? 0 : Math.Clamp((((x - ax) * dx) + ((y - ay) * dy)) / length, 0, 1);
        return DistanceD(ax + (t * dx), ay + (t * dy), x, y) < radius;
    }

    /// <summary>
    /// Where a shot that does not home aims: along the target's
    /// heading, the step at which shot and target come closest. Quirk kept:
    /// it aims two steps past that meeting.
    /// </summary>
    private void LeadAim(Shot shot, BattleObject target)
    {
        var v = target.Speed;
        var th = target.Facing;
        if (v == 0.0)
        {
            shot.Goal = BattleGoal.At(target.PosX, target.PosY);
            shot.Facing = X87.ToInt(DirectionD(shot.PosX, shot.PosY, target.PosX, target.PosY));
            return;
        }

        var best = 999999999.0;
        var k = 1;
        while (true)
        {
            var p = Along(target, th, v * k);
            var h = X87.ToInt(DirectionD(shot.PosX, shot.PosY, p.X, p.Y));
            var q = Along(shot, h, k * shot.Speed);
            var d = DistanceD(q.X, q.Y, p.X, p.Y);
            k = (k + 1) & 0xFF;
            if (d > best)
            {
                break;
            }

            best = d;
        }

        // The fix: the step found closest, two back from where the loop stops.
        var aim = Along(target, th, (Fixed(Fixes.ShotsLeadTrue) ? Math.Max(1, k - 2) : k) * v);
        shot.Goal = BattleGoal.At(aim.X, aim.Y);
        shot.Facing = X87.ToInt(DirectionD(shot.PosX, shot.PosY, aim.X, aim.Y));
    }

    /// <summary>
    /// A shot's step. A missile speeds up and turns toward its
    /// goal (5 degrees, 6 to the left). A shot that missed dies at its goal.
    /// One with a target asks whether it has reached it: not yet, it moves; all
    /// rounds missed, it flies on without a target (a missile on a new heading
    /// drawn at random); some hit, they land on the target and the rest fly on.
    /// </summary>
    private void MoveShot(Battle battle, Shot shot, List<Shot> list, ref StepEvents events)
    {
        var g = shot.Goal?.Point ?? (shot.PosX, shot.PosY);
        if (shot.Tracking)
        {
            var sum = shot.Speed + Shot.Acceleration;
            shot.Speed = shot.TopSpeed <= sum ? shot.TopSpeed : sum;
            var h = X87.ToInt(DirectionD(shot.PosX, shot.PosY, g.X, g.Y));
            if (h != shot.Facing)
            {
                var diff = h - shot.Facing;
                var dir = 1;
                if (diff < 0)
                {
                    diff += 360;
                }

                if (diff > 180)
                {
                    diff = 360 - diff;
                    dir = -1;
                }

                var step = Shot.MissileTurnRate <= diff ? X87.ToInt(dir * Shot.MissileTurnRate) : dir * diff;
                shot.Facing = WrapDegrees(shot.Facing + step);
            }
        }

        if (shot.Target is not { } target || shot.Missed)
        {
            if (DistanceD(shot.X, shot.Y, g.X, g.Y) <= shot.Speed)
            {
                shot.Speed = 0.0;
                RemoveShot(battle, list, shot);
                ShotDies(battle, shot, explode: true);
                return;
            }

            Fly(shot);
            return;
        }

        var hits = HitTest(battle, target, shot, ref events);
        if (hits == 0)
        {
            Fly(shot);
            return;
        }

        target.AimedAt--;
        if (hits < 0)
        {
            shot.Missed = true;
            if (shot.Tracking)
            {
                var a = WrapDegrees(Random.Next(100) - 50 + shot.Facing);
                var to = Along(shot, a, MissDistance);
                shot.Goal = BattleGoal.At(to.X, to.Y);
            }

            return;
        }

        if (shot.Rounds <= hits)
        {
            RemoveShot(battle, list, shot);
            target.Incoming.Insert(0, shot);
            return;
        }

        // The rounds that hit go on as a shot of their own, straight onto
        // the target (quirk kept: it is aimed at the target once more).
        var split = NewShot(shot.Owner, shot.Tech, false, shot.X, shot.Y, target, hits, 0, shot.Power);
        target.Incoming.Insert(0, split);
        shot.Rounds -= hits;
        shot.Missed = true;
        if (shot.Tracking)
        {
            var to = Along(shot, shot.Facing, MissDistance);
            shot.Goal = BattleGoal.At(to.X, to.Y);
        }
    }

    private void Fly(Shot shot)
    {
        var next = Along(shot, shot.Facing, shot.Speed);
        shot.Flown += shot.Speed;
        shot.SetPosition(next.X, next.Y);
    }

    private static void RemoveShot(Battle battle, List<Shot> list, Shot shot)
    {
        if (list.Remove(shot))
        {
            battle.LiveShots--;
        }
    }

    /// <summary>
    /// Whether a shot has reached what it was fired at, and how many of its
    /// rounds hit: 0 not yet, -1 all missed. One roll of 100 a round.
    /// </summary>
    private int HitTest(Battle battle, BattleObject target, Shot shot, ref StepEvents events)
    {
        switch (target)
        {
            // beams against hull defense, missiles against ECM.
            case BattleShip ship:
                if (ship.Radius >> 1 <= DistanceD(ship.PosX, ship.PosY, shot.X, shot.Y))
                {
                    return 0;
                }

                WarOnFire(battle, ship.Owner, shot.Owner, ref events);
                return Rolls(shot, shot.Tracking
                    ? (sbyte)shot.Tech.WeaponFlag - ship.Ship.EcmRating() + 100
                    : (short)(shot.Accuracy + shot.Tech.WeaponAccuracy) - ship.Ship.HullDefense() + 50);

            // no roll, every round lands. Quirk kept: the
            // radius is small, so a fast shot can step over the block and fly on.
            case PlanetBlock block:
                if (DistanceD(block.PosX, block.PosY, shot.X, shot.Y) >= PlanetBlock.HitRadius
                    && !(Fixed(Fixes.ShotsHitPlanets) && PassesWithin(shot, block.PosX, block.PosY, PlanetBlock.HitRadius)))
                {
                    return 0;
                }

                WarOnFire(battle, PlanetOwner(battle), shot.Owner, ref events);
                return shot.Rounds;

            case Orbital orbital:
                if (orbital.Size >> 1 <= DistanceD(orbital.PosX, orbital.PosY, shot.X, shot.Y))
                {
                    return 0;
                }

                WarOnFire(battle, orbital.Owner, shot.Owner, ref events);
                return Rolls(shot, shot.Tracking ? (sbyte)shot.Tech.WeaponFlag + 100 : (short)(shot.Accuracy + shot.Tech.WeaponAccuracy) + 50);

            case Fighter fighter:
                return FighterHitTest(fighter, shot);

            case Mine mine:
                return MineHitTest(battle, mine, shot);

            // point defense reaching a missile, no defense and no floor.
            case Shot missile:
                if (DistanceD(missile.PosX, missile.PosY, shot.X, shot.Y) >= 10.0)
                {
                    return 0;
                }

                var hits = 0;
                var chance = shot.Tracking ? 100 : (short)(shot.Accuracy + shot.Tech.WeaponAccuracy) + 50;
                for (var i = 0; i < shot.Rounds; i++)
                {
                    if (Random.Next(100) < chance)
                    {
                        hits++;
                    }
                }

                return hits == 0 ? -1 : hits;
            default:
                return 0;
        }
    }

    /// <summary>A roll of 100 a round under the chance, at least 5.</summary>
    private int Rolls(Shot shot, int chance)
    {
        if (chance < 6)
        {
            chance = 5;
        }

        var hits = 0;
        for (var i = 0; i < shot.Rounds; i++)
        {
            if (Random.Next(100) < chance)
            {
                hits++;
            }
        }

        return hits == 0 ? -1 : hits;
    }

    /// <summary>
    /// A shot reaching something of a player not at war with the shooter
    /// declares war both ways; the side shot at weighs giving
    /// up afresh and thinks again at once (with no retreat roll).
    /// </summary>
    private void WarOnFire(Battle battle, int target, int shooter, ref StepEvents events)
    {
        if (target == shooter || target < 0 || target >= Players.Count || shooter >= Players.Count || Players[target].Treaties[shooter].Level() == Treaty.War)
        {
            return;
        }

        SetTreaty(Players[target], Players[shooter], Treaty.War, Treaty.InForce, ref events);
        SetTreaty(Players[shooter], Players[target], Treaty.War, Treaty.InForce, ref events);
        if (battle.Commanders[target] is { } commander)
        {
            Disengage(battle, commander);
            ForceThink(battle, commander);
        }
    }

    /// <summary>
    /// Shots that reached a ship land, each round in turn:
    /// beams through its shield, then a roll of 1000 decides whether a
    /// component takes the hit first, passing on what it could not take to
    /// the hull. A kill credits the shooter with a tenth of the ship.
    /// </summary>
    internal void ResolveShipHits(Battle battle, BattleShip ship, ref StepEvents events)
    {
        if (ship.Dead)
        {
            return;
        }

        foreach (var h in ship.Incoming)
        {
            for (var round = 0; round < h.Rounds && !ship.Dead; round++)
            {
                var x = h.RoundDamage();
                if (!h.Tracking)
                {
                    var through = ship.Ship.ShieldAbsorb(x);
                    if (through < x)
                    {
                        ShieldFlash(battle, ship, h);
                    }

                    x = through;
                }

                if (x == 0)
                {
                    continue;
                }

                battle.LastHit = battle.Steps;
                var threshold = ship.Ship.ComponentHitThreshold();
                if (Random.Next(1000) <= threshold)
                {
                    x = ship.Ship.HitComponent(Random, x);
                    ship.AfterComponentHit();
                }

                if (x != 0)
                {
                    ship.Ship.Structure -= x;
                    if (ship.Ship.Structure <= 0)
                    {
                        if (battle.Commanders[h.Owner] is { } shooter)
                        {
                            shooter.Kills += ship.Ship.MaxStructure / 10;
                        }

                        ShipKilled(ship, h.Owner);
                        TacticalShipDies(battle, ship);
                    }
                }

                StrikeFlash(battle, ship, h);
            }

            h.Speed = 0.0;
            ShotDies(battle, h, explode: false);
        }

        ship.Incoming.Clear();
        MineContact(battle, ship, ref events);
    }

    /// <summary>
    /// A ship destroyed: its owner grows more cautious when it
    /// was the fleet's last ship, and the shooter's player gains an incident.
    /// </summary>
    private void ShipKilled(BattleShip victim, int shooter)
    {
        var owner = Players[victim.Owner];
        if (victim.Fleet.Ships.Count == 1)
        {
            owner.Caution -= -0.2;
        }

        if (shooter < Players.Count)
        {
            Incident(Players[shooter], owner, IncidentKind.ShipDestroyed);
        }
    }

    /// <summary>A ship dies in a battle fought step by step: an explosion, its footprint freed, gone from its fleet and the field.</summary>
    private void TacticalShipDies(Battle battle, BattleShip ship)
    {
        if (ship.Dead)
        {
            return;
        }

        ShipExplosion(battle, ship);
        CarrierGone(battle, ship);
        battle.ClearFootprint(ship.X, ship.Y, Half(ship));
        ship.Dead = true;
        ship.Fleet.RemoveShip(ship.Ship, Tables.Tech);
        Happen(new ModEvent(ModEventKind.ShipDestroyed, Players[ship.Owner], Ship: ship.Ship));
        ship.Ship.InBattle = false;
        battle.Ships[ship.Owner].Remove(ship);
    }

    /// <summary>A shot dies; a missile that dies in flight explodes.</summary>
    private void ShotDies(Battle battle, Shot shot, bool explode)
    {
        if (shot.Dead)
        {
            return;
        }

        shot.Dead = true;
        if (shot.Tracking && explode)
        {
            AddEffect(battle.OrbitalEffects, MissileBlast, shot.PosX, shot.PosY, shot.Facing, shot.Speed);
        }
    }

    /// <summary>
    /// A shot's post-step. Its target dead, it flies on 500
    /// along its heading (a missile turned at random); out of range (damage
    /// below 1), it dies. Quirk kept: a weapon too weak to do a point of
    /// damage dies on its first post-step.
    /// </summary>
    private void PostStepShot(Battle battle, Shot shot, List<Shot> list)
    {
        if (shot.Target is { Dead: true } target)
        {
            if (target is PlanetBlock)
            {
                shot.Goal = BattleGoal.At(target.X, target.Y);
            }
            else
            {
                var a = shot.Facing;
                if (shot.Tracking)
                {
                    a = WrapDegrees(a + Random.Next(100) - 50);
                }

                var to = Along(shot, a, MissDistance);
                shot.Goal = BattleGoal.At(to.X, to.Y);
            }

            shot.Target = null;
            shot.Missed = true;
        }

        if (shot.RoundDamage() < 1)
        {
            if (!shot.Missed && shot.Target is { } aimed)
            {
                aimed.AimedAt--;
            }

            RemoveShot(battle, list, shot);
            ShotDies(battle, shot, explode: true);
        }
    }

    /// <summary>
    /// Point defense that reached a missile: each round strips
    /// its hit points; each time they run out a missile of the salvo goes,
    /// and with the last the shot dies.
    /// </summary>
    private void InterceptMissile(Battle battle, Shot missile)
    {
        foreach (var h in missile.Incoming)
        {
            for (var round = 0; round < h.Rounds && !missile.Dead; round++)
            {
                missile.MissileHitPoints -= h.RoundDamage();
                if (missile.MissileHitPoints < 1)
                {
                    missile.Rounds--;
                    AddEffect(battle.ShipEffects, MissileBlast, missile.PosX, missile.PosY, missile.Facing, missile.Speed);
                    missile.MissileHitPoints = missile.Tech.MissileHitPoints;
                    if (missile.Rounds == 0)
                    {
                        RemoveShot(battle, battle.Missiles[missile.Owner], missile);
                        ShotDies(battle, missile, explode: true);
                    }
                }

                h.Speed = 0.0;
            }

            ShotDies(battle, h, explode: false);
        }

        missile.Incoming.Clear();
    }
}
