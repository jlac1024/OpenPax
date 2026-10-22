using OpenPax.Sim.Combat;
using OpenPax.Sim.Numerics;
using OpenPax.Sim.Players;

namespace OpenPax.Sim;

/// <summary>Resolving a battle at once, ending it and letting its fleets go (docs/sim/combat.md).</summary>
public sealed partial class Game
{
    private const int LeaveAfterRounds = 3;

    /// <summary>
    /// Last resort for battles that still find no end (for example a
    /// shield that regains all it soaks each round): every side withdraws.
    /// </summary>
    private const int MaxRounds = 100000;

    private void AutoResolve(Battle battle, ref StepEvents events)
    {
        // Fighters take no part when resolving at once; mines lie still.
        foreach (var list in battle.Fighters)
        {
            list.Clear();
        }

        for (var round = 0; ; round++)
        {
            for (var p = 0; p < Battle.Players; p++)
            {
                if (battle.Commanders[p] is { } commander)
                {
                    ForceThink(battle, commander);
                }
            }

            for (var p = 0; p < Battle.Players; p++)
            {
                foreach (var ship in battle.Ships[p].ToList())
                {
                    if (ship.Dead)
                    {
                        continue;
                    }

                    if (ship.State == BattleShip.Leaving && round > LeaveAfterRounds)
                    {
                        Leave(battle, ship);
                    }
                    else
                    {
                        ShipStep(battle, ship, ref events);
                    }
                }

                foreach (var orbital in battle.Orbitals[p].ToList())
                {
                    if (!orbital.Dead)
                    {
                        OrbitalStep(battle, orbital);
                    }
                }
            }

            PostStep(battle);
            if (BattleOver(battle))
            {
                break;
            }

            // Port decision (docs/decisions.md): the original loops for ever
            // when nobody can hurt anybody; here every side withdraws.
            if (Stalemate(battle) || round >= MaxRounds)
            {
                WithdrawAll(battle);
                BattleOver(battle);
                break;
            }
        }

        // Experience again after the loop (quirk kept: twice in all; once with the fix).
        if (!Fixed(Fixes.ExperienceOnce))
        {
            GiveExperience(battle);
        }
    }

    /// <summary>
    /// Nobody can hurt anybody any more: no explosion is still dying down,
    /// no ship is about to leave, and no ship or orbital has something it
    /// can damage.
    /// </summary>
    private bool Stalemate(Battle battle)
    {
        if (battle.OrbitalEffects.Count > 0 || battle.ShipEffects.Count > 0)
        {
            return false;
        }

        for (var p = 0; p < Battle.Players; p++)
        {
            foreach (var ship in battle.Ships[p])
            {
                // Only a computer side still fighting picks targets for its idle ships.
                var picks = battle.Commanders[p] is { Computer: true, Done: false };
                if (ship.State == BattleShip.Leaving || (ship.State == BattleShip.Idle && picks && PickTarget(battle, p, ship) is not null))
                {
                    return false;
                }

                if (ship.Target is { Dead: false } target && ship.Ship.AutoDamage(Tables.Tech) > 0 && CanBeHurt(battle, target))
                {
                    return false;
                }
            }

            foreach (var orbital in battle.Orbitals[p])
            {
                var enemyShips = Enumerable.Range(0, Battle.Players).Any(i => AtWar(p, i) && battle.Ships[i].Count > 0);
                if (enemyShips && orbital.Damage(Tables.Tech) > 0)
                {
                    return false;
                }
            }
        }

        return true;
    }

    /// <summary>Whether a hit on the target can change anything: a block only while an improvement is under way.</summary>
    private static bool CanBeHurt(Battle battle, BattleObject target)
    {
        return target is not PlanetBlock
            || battle.Planet?.Colony?.Improvements.Any(i => i is not null && i.Percent > 0.0) == true;
    }

    /// <summary>Every ship left on the field leaves it; the fleets go home retreating at teardown.</summary>
    private void WithdrawAll(Battle battle)
    {
        for (var p = 0; p < Battle.Players; p++)
        {
            foreach (var ship in battle.Ships[p].ToList())
            {
                Leave(battle, ship);
            }
        }
    }

    /// <summary>At war: the highest treaty in force between two player slots is war.</summary>
    private bool AtWar(int p, int i)
    {
        return i < Players.Count && p < Players.Count && Players[p].Treaties[i].Level() == Treaty.War;
    }

    /// <summary>The colony owner's index, or -1 with no planet or colony.</summary>
    private static int PlanetOwner(Battle battle)
    {
        return battle.Planet?.Colony?.Owner.Index ?? -1;
    }

    /// <summary>
    /// Enemy ships, then enemy orbitals, nearest and strongest scoring
    /// best. Quirk kept: the first candidate is taken with no score, so any
    /// later one with a positive score replaces it. With none, the colony's
    /// first block.
    /// </summary>
    private BattleObject? PickTarget(Battle battle, int p, BattleShip shooter)
    {
        BattleObject? pick = null;
        var best = 0;

        int Score(BattleObject o, int bonus)
        {
            var dx = o.X - shooter.X;
            var dy = o.Y - shooter.Y;
            return 10000 - (int)Math.Sqrt(dx * dx + dy * dy) + bonus;
        }

        void Consider(BattleObject o, int bonus)
        {
            if (o.Dead)
            {
                return;
            }

            if (pick is null)
            {
                pick = o;
                return;
            }

            var score = Score(o, bonus);
            if (score > best)
            {
                pick = o;
                best = score;
            }
        }

        for (var i = 0; i < Battle.Players; i++)
        {
            if (i != p && AtWar(p, i))
            {
                foreach (var s in battle.Ships[i])
                {
                    Consider(s, s.Dead ? 0 : 2 * s.Ship.Strength(Tables.Tech));
                }
            }
        }

        for (var i = 0; i < Battle.Players; i++)
        {
            if (i != p && AtWar(p, i))
            {
                foreach (var o in battle.Orbitals[i])
                {
                    Consider(o, o.Structure.Tech.Kind is TechInfo.ShipyardKind or TechInfo.TrainingBaseKind ? 0 : 50 * o.Count);
                }
            }
        }

        if (pick is null)
        {
            var q = PlanetOwner(battle);
            if (q >= 0 && q != p && AtWar(p, q))
            {
                pick = battle.Blocks.FirstOrDefault(b => !b.Dead);
            }
        }

        return pick;
    }

    /// <summary>A shot at the target: hits when R(100) is below attack - defense + 50 (at least 1).</summary>
    private void ShipStep(Battle battle, BattleShip ship, ref StepEvents events)
    {
        if (ship.Target is not { Dead: false } target)
        {
            return;
        }

        var attack = ship.Ship.CombatAttack();
        var defense = target is BattleShip ts ? ts.Ship.CombatDefense() : 0;
        var damage = ship.Ship.AutoDamage(Tables.Tech);
        var chance = Math.Max(1, attack - defense + 50);
        if (Random.Next(100) < chance)
        {
            switch (target)
            {
                case BattleShip victim:
                    DamageShip(battle, victim, damage);
                    break;
                case Orbital orbital:
                    DamageOrbital(battle, orbital, damage, ref events);
                    break;
                case PlanetBlock block:
                    DamageBlock(battle, block, damage, ref events);
                    break;
            }
        }
    }

    /// <summary>An orbital fires at the first ship of the lowest numbered enemy side.</summary>
    private void OrbitalStep(Battle battle, Orbital orbital)
    {
        for (var i = 0; i < Battle.Players; i++)
        {
            if (AtWar(orbital.Owner, i) && battle.Ships[i].Count > 0)
            {
                var target = battle.Ships[i][0];
                var chance = Math.Max(1, 100 - target.Ship.CombatDefense() + 50);
                if (Random.Next(100) < chance)
                {
                    DamageShip(battle, target, orbital.Damage(Tables.Tech));
                }

                return;
            }
        }
    }

    /// <summary>
    /// Damage in chunks of 50, then the rest (always one more roll): one in
    /// twenty lands on a component, the rest on the hull.
    /// </summary>
    private void DamageShip(Battle battle, BattleShip ship, int damage)
    {
        for (var chunk = 0; chunk < damage / 50 && !ship.Dead; chunk++)
        {
            Hit(battle, ship, 50);
        }

        if (!ship.Dead)
        {
            Hit(battle, ship, damage % 50);
        }
    }

    private void Hit(Battle battle, BattleShip ship, int amount)
    {
        if (Random.Next(20) == 0)
        {
            ship.Ship.HitComponent(Random, amount);
            return;
        }

        ship.Ship.Structure -= amount;
        if (ship.Ship.Structure <= 0)
        {
            ShipDies(battle, ship);
        }
    }

    /// <summary>The ship is lost from its fleet; its explosion keeps the battle going 35 more rounds.</summary>
    // (no incident or caution in a battle resolved at once)
    private void ShipDies(Battle battle, BattleShip ship)
    {
        ShipExplosion(battle, ship);
        ship.Dead = true;
        ship.Fleet.RemoveShip(ship.Ship, Tables.Tech);
        Happen(new ModEvent(ModEventKind.ShipDestroyed, Players[ship.Owner], Ship: ship.Ship));
        ship.Ship.InBattle = false;
        battle.Ships[ship.Owner].Remove(ship);
    }

    /// <summary>Through the shield (weakened by subsystem damage), then onto the structure in chunks of 50.</summary>
    private void DamageOrbital(Battle battle, Orbital orbital, int damage, ref StepEvents events)
    {
        var s = orbital.Structure;
        damage = OrbitalShield(s, damage);
        for (var chunk = 0; chunk < damage / 50; chunk++)
        {
            if (!HitStructure(s, 50.0, ref events))
            {
                OrbitalDies(battle, orbital);
                return;
            }
        }

        if (!HitStructure(s, damage % 50, ref events))
        {
            OrbitalDies(battle, orbital);
        }
    }

    /// <summary>What gets through a structure's shield, weakened by damage to its first part; the rest it soaks up.</summary>
    private static int OrbitalShield(Structure s, int damage)
    {
        if (s.ShieldTech is { } shield)
        {
            var f = 1.0 - s.Count * (s.SubsystemDamage[0] / shield.HitPoints);
            if (f > 0.0)
            {
                var cap = (int)(shield.ShieldCapacity * f);
                var percent = shield.ShieldPercent * f;
                if (percent != 0.0)
                {
                    damage = (int)((1.0 - percent * 0.01) * damage + 0.5);
                }

                if (s.ShieldAbsorbed < cap)
                {
                    var take = Math.Min(damage, cap - (int)s.ShieldAbsorbed);
                    damage -= take;
                    s.ShieldAbsorbed += take;
                }
            }
        }

        return damage;
    }

    // (no incident in a battle resolved at once)
    private void OrbitalDies(Battle battle, Orbital orbital)
    {
        OrbitalExplosion(battle, orbital);
        orbital.Dead = true;
        battle.Orbitals[orbital.Owner].Remove(orbital);
    }

    /// <summary>A block struck when resolving at once: the colony takes the damage, and the block falls once no band is left.</summary>
    private void DamageBlock(Battle battle, PlanetBlock block, int damage, ref StepEvents events)
    {
        if (block.Dead || damage == 0 || battle.Planet?.Colony is not { } colony)
        {
            return;
        }

        DamageColony(colony, damage, ref events);
        battle.Bands = colony.Destroyed ? -1 : Bands(colony);
        if (battle.Bands < 0)
        {
            block.Dead = true;
        }
    }

    /// <summary>
    /// Bombs strike a colony: the first improvement under way
    /// takes all of it (a finished one that drops below done loses its
    /// effect), and population shrinks with what still stands.
    /// </summary>
    private void DamageColony(Colony colony, double damage, ref StepEvents events)
    {
        var before = Standing(colony);
        if (colony.Improvements.FirstOrDefault(i => i is not null && i.Percent > 0.0) is { } improvement)
        {
            var wasComplete = improvement.Complete;
            improvement.Built = Math.Max(0.0, improvement.Built - damage);
            if (wasComplete && !improvement.Complete)
            {
                ApplyImprovement(colony, improvement, false);
            }
        }

        var after = Standing(colony);
        SetColonyResource(colony, Colony.Population, (double)after / before * colony.CurrentPopulation, ref events);
    }

    /// <summary>The ship leaves the field: its fleet is set retreating, its orders dropped, a new orbit drawn.</summary>
    private void Leave(Battle battle, BattleShip ship)
    {
        battle.Ships[ship.Owner].Remove(ship);
        battle.Graveyard.Insert(0, ship);
        ship.Fleet.Retreating = true;
        ClearOrders(ship.Fleet);
        ship.Fleet.EnterOrbit(Random, null);
        ship.Dead = true;
    }

    private void PostStep(Battle battle)
    {
        for (var p = 0; p < Battle.Players; p++)
        {
            foreach (var ship in battle.Ships[p])
            {
                // a dead target is dropped; a pursuer goes idle to pick another.
                if (ship.Target is { Dead: true })
                {
                    if (ship.State == BattleShip.Pursuing)
                    {
                        ship.State = BattleShip.Idle;
                    }

                    ship.Target = null;
                }
            }

            foreach (var orbital in battle.Orbitals[p])
            {
                RegainShield(orbital);
            }
        }

        BlocksFall(battle);
        PostStepEffects(battle);
    }

    /// <summary>An orbital's shield regains some of what it soaked.</summary>
    private static void RegainShield(Orbital orbital)
    {
        var s = orbital.Structure;
        if (s.ShieldTech is { } shield)
        {
            var f = 1.0 - s.Count * (s.SubsystemDamage[0] / shield.HitPoints);
            if (f > 0.0)
            {
                s.ShieldAbsorbed -= Math.Min(s.ShieldAbsorbed, (int)(shield.ShieldRegeneration * f));
            }
        }
    }

    /// <summary>The planet's blocks above the bands still standing fall.</summary>
    private static void BlocksFall(Battle battle)
    {
        var bands = battle.Planet?.Colony is { } colony ? Bands(colony) : -1;
        foreach (var block in battle.Blocks)
        {
            if (!block.Dead && block.Index > bands)
            {
                block.Dead = true;
            }
        }
    }

    /// <summary>
    /// Over when no ship is left anywhere; or, once explosions have died
    /// down, when fewer than two players are still present.
    /// </summary>
    // (as it runs when resolving at once)
    private bool BattleOver(Battle battle)
    {
        if (Enumerable.Range(0, Battle.Players).All(p => battle.Ships[p].Count == 0))
        {
            return true;
        }

        return OneSideLeft(battle);
    }

    /// <summary>
    /// Once explosions have died down, over when fewer than two players are
    /// still present (part B): every ship gains experience and its
    /// fleet is let go, no longer retreating. The colony's bands are counted
    /// afresh. Fighters count, docked or out; mines do not.
    /// </summary>
    private bool OneSideLeft(Battle battle)
    {
        if (battle.OrbitalEffects.Count > 0 || battle.ShipEffects.Count > 0)
        {
            return false;
        }

        var owner = PlanetOwner(battle);
        var bands = Bands(battle.Planet?.Colony);
        var present = Enumerable.Range(0, Battle.Players).Count(p =>
            (p == owner && bands >= 0) || battle.Ships[p].Count > 0 || battle.Orbitals[p].Count > 0
            || battle.Shots[p].Count > 0 || battle.Missiles[p].Count > 0 || battle.Fighters[p].Count > 0);
        if (present >= 2)
        {
            return false;
        }

        GiveExperience(battle);
        foreach (var ship in Enumerable.Range(0, Battle.Players).SelectMany(p => battle.Ships[p]))
        {
            ship.Fleet.Retreating = false;
            Release(ship.Fleet);
        }

        return true;
    }

    /// <summary>Every ship still on the field gains a point of experience.</summary>
    // (the kill tally stays 0 when resolving at once)
    private static void GiveExperience(Battle battle)
    {
        for (var p = 0; p < Battle.Players; p++)
        {
            if (battle.Commanders[p] is null)
            {
                continue;
            }

            // Kills shared out over the side's ships, plus one each (0 kills when resolved at once).
            var n = battle.Ships[p].Count;
            foreach (var ship in battle.Ships[p])
            {
                if (!ship.Dead)
                {
                    ship.Ship.AddExperience(battle.Commanders[p]!.Kills / n + 1); // the commander was just tested
                }
            }
        }
    }

    /// <summary>The battle is taken down: population checked against its maximum, fleets that left sent home.</summary>
    private void EndBattle(Battle battle, ref StepEvents events)
    {
        if (battle.Planet?.Colony is { } colony)
        {
            SetColonyResource(colony, Colony.Population, colony.CurrentPopulation, ref events);
            if (!colony.Destroyed)
            {
                SetColonyResource(colony, Colony.Population, colony.CurrentPopulation, ref events);
            }
        }

        if (battle.Planet is { } planet)
        {
            planet.InCombat = false;
        }

        foreach (var ship in battle.Graveyard)
        {
            if (ship.Fleet.InCombat)
            {
                Release(ship.Fleet);
            }

            ship.Ship.InBattle = false;
        }

        foreach (var ship in Enumerable.Range(0, Battle.Players).SelectMany(p => battle.Ships[p]))
        {
            // Only reached when the round guard ended the battle.
            if (ship.Fleet.InCombat)
            {
                Release(ship.Fleet);
            }

            ship.Ship.InBattle = false;
        }

        Battles.Remove(battle);
    }

    /// <summary>
    /// A fleet is let go. A fleet that retreated raises its owner's caution
    /// and heads for the nearest colony in reach (its own, else a friend's),
    /// else through a wormhole drawn at random.
    /// </summary>
    private void Release(Fleet fleet)
    {
        fleet.InCombat = false;
        fleet.UpdateStats(Tables.Tech);
        if (!fleet.Retreating)
        {
            return;
        }

        fleet.Owner.Caution -= -0.2;
        var home = NearestColony(fleet, fleet.Owner);
        if (home is null)
        {
            foreach (var other in Players)
            {
                // Quirk kept: no contact (200) counts as friendly here.
                if (other != fleet.Owner && fleet.Owner.Treaties[other.Index].Level() > 1
                    && NearestColony(fleet, other) is { } friendly)
                {
                    home = friendly;
                    break;
                }
            }
        }

        if (home is not null && PlanRoute(fleet, home.Star))
        {
            GiveOrder(fleet, Order.To(home.Planet));
        }
        else
        {
            var star = Universe.Stars[fleet.Star];
            if (star.Wormholes.Count > 0)
            {
                var hole = star.Wormholes[Random.Next(star.Wormholes.Count)];
                if (hole.Partner is { } partner && RangeOk(fleet, [fleet.Star, partner.Star]))
                {
                    GiveOrder(fleet, Order.To(hole));
                }
                else
                {
                    fleet.Retreating = false;
                }
            }
        }

        fleet.BoostTicks = 9;
    }

    /// <summary>A player's colony in another system, fewest jumps away (first on ties), if within the fleet's jumps left.</summary>
    private Colony? NearestColony(Fleet fleet, Player player)
    {
        Colony? best = null;
        var bestHops = 0;
        foreach (var c in player.Colonies)
        {
            if (c.Star == fleet.Star)
            {
                continue;
            }

            var hops = Universe.Hops(fleet.Star, c.Star);
            if (best is null || hops < bestHops)
            {
                best = c;
                bestHops = hops;
            }
        }

        return best is not null && bestHops <= fleet.JumpsLeft ? best : null;
    }
}
