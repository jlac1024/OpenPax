using OpenPax.Sim.Combat;
using OpenPax.Sim.Players;

namespace OpenPax.Sim;

/// <summary>
/// Orbitals in a battle fought step by step (docs/sim/combat-step.md): only
/// missile bases, battle stations and fighter bases look and fire. They aim
/// from the planet's corner of the field, with no range or arc. No draws
/// here; hits draw in the shot's flight and in the structure's damage.
/// </summary>
public sealed partial class Game
{
    private const double FieldCorner = 3600.0;
    private const int OrbitalAccuracy = 100;
    private const int OrbitalPower = 150;
    private const int NoPointDefenseTarget = -1000;

    private static bool OrbitalActs(Orbital orbital) =>
        orbital.Structure.Tech.Kind is TechInfo.MissileBaseKind or TechInfo.BattleStationKind or TechInfo.FighterBaseKind;

    /// <summary>Whole distance from the field's far corner.</summary>
    private static int CornerDistance(BattleObject o) => (int)DistanceD(o.PosX, o.PosY, FieldCorner, FieldCorner);

    /// <summary>
    /// An orbital's sensor takes one more enemy ship to follow,
    /// the one nearest the field's far corner, anywhere on the field. Quirk
    /// kept: the distance is from the corner, not from the orbital; with no
    /// sensor it follows nothing.
    /// </summary>
    internal void ScanOrbital(Battle battle, Orbital orbital)
    {
        if (!OrbitalActs(orbital) || orbital.Tracked.Count == orbital.TrackLimit || orbital.Dead)
        {
            return;
        }

        BattleShip? best = null;
        var bestScore = 0;
        for (var q = 0; q < Battle.Players; q++)
        {
            if (!AtWar(orbital.Owner, q))
            {
                continue;
            }

            foreach (var ship in battle.Ships[q])
            {
                if (orbital.Tracked.Contains(ship) || ship.Dead)
                {
                    continue;
                }

                var score = 10000 - CornerDistance(ship);
                if (best is null || score > bestScore)
                {
                    (best, bestScore) = (ship, score);
                }
            }
        }

        if (best is not null)
        {
            orbital.Tracked.Insert(0, best);
        }
    }

    /// <summary>
    /// An orbital fires. Each shot carries one round per 25
    /// percent of the stack left (at least one), at full accuracy and half
    /// again the damage, reaching 2000. The second energy weapon is point
    /// defense against missiles near it; the two weapons fire at the ordered
    /// target within 1500, else at the followed ship it likes best.
    /// </summary>
    internal void FireOrbital(Battle battle, Orbital orbital)
    {
        if (orbital.Dead || !OrbitalActs(orbital))
        {
            return;
        }

        var s = orbital.Structure;
        var rounds = Math.Max(1, (int)(s.Percent * 0.04));
        if (s.SecondEnergyTech is { } pd)
        {
            if (s.Reload[2] <= 0)
            {
                BattleObject? best = null;
                var bestScore = 0;
                for (var q = 0; q < Battle.Players; q++)
                {
                    if (!AtWar(orbital.Owner, q))
                    {
                        continue;
                    }

                    // Quirk kept: no test for dead.
                    foreach (var o in battle.Missiles[q].Cast<BattleObject>().Concat(battle.Fighters[q]))
                    {
                        var v = OrbitalPointDefenseScore(orbital, o);
                        if (v != NoPointDefenseTarget && v > bestScore)
                        {
                            (best, bestScore) = (o, v);
                        }
                    }
                }

                if (best is not null)
                {
                    OrbitalShot(battle, orbital, pd, best, rounds, true);
                    s.TakeVolley(2);
                }
            }
            else
            {
                s.Reload[2]--;
            }
        }

        var w1 = s.WeaponTechs[0];
        var w2 = s.WeaponTechs[1];
        var ready1 = w1 is not null && s.Reload[0] <= 0;
        var ready2 = w2 is not null && s.Reload[1] <= 0;
        BattleObject? target = null;
        if (ready1 || ready2)
        {
            if (orbital.Ordered is { } ordered && DistanceD(orbital.PosX, orbital.PosY, ordered.PosX, ordered.PosY) <= FireRange)
            {
                target = ordered;
            }
            else
            {
                var best = -500;
                foreach (var o in orbital.Tracked)
                {
                    var score = OrbitalTargetScore(orbital, o);
                    if (score > best)
                    {
                        (target, best) = (o, score);
                    }
                }
            }
        }

        for (var slot = 0; slot < 2; slot++)
        {
            if (!(slot == 0 ? ready1 : ready2))
            {
                s.Reload[slot]--;
            }
            else if (target is not null)
            {
                OrbitalShot(battle, orbital, s.WeaponTechs[slot]!, target, rounds, false); // ready means the weapon is fitted
                s.TakeVolley(slot);
                orbital.TargetBefore = orbital.LastTarget;
                orbital.LastTarget = target;
            }
        }

        if (orbital.AutoLaunch)
        {
            if (orbital.LaunchCountdown == 0)
            {
                if ((orbital.Ordered ?? orbital.Tracked.FirstOrDefault()) is { } launchAt)
                {
                    LaunchFromOrbital(battle, orbital, launchAt);
                    orbital.LaunchCountdown = 5;
                }
            }
            else
            {
                orbital.LaunchCountdown--;
            }
        }
    }

    /// <summary>
    /// How much point defense wants a missile or fighter:
    /// within 320, the nearer the better, most of all a missile coming at
    /// this orbital.
    /// </summary>
    private static int OrbitalPointDefenseScore(Orbital orbital, BattleObject o)
    {
        var d = Numerics.X87.ToInt(DistanceD(o.PosX, o.PosY, orbital.PosX, orbital.PosY) - -0.5);
        if (d > 320)
        {
            return NoPointDefenseTarget;
        }

        return o switch
        {
            Shot { } missile when missile.Target == orbital => 420 - d,
            Shot { Tracking: true } missile when missile.Tech.WeaponType != 0 => 520 - d,
            Fighter { } f when f.Link == orbital => 370 - d,
            _ => 320 - d,
        };
    }

    /// <summary>
    /// How much an orbital wants a followed ship: nearer the
    /// corner is better, and one it did not just shoot at better still.
    /// </summary>
    private static int OrbitalTargetScore(Orbital orbital, BattleObject o)
    {
        var bonus = o == orbital.LastTarget ? 1800 : o == orbital.TargetBefore ? 1900 : 2000;
        return -CornerDistance(o) + bonus;
    }

    /// <summary>An orbital's shot, from the orbital at full accuracy and power 150.</summary>
    private void OrbitalShot(Battle battle, Orbital orbital, TechInfo tech, BattleObject target, int rounds, bool pointDefense)
    {
        Launch(battle, NewShot(orbital.Owner, tech, pointDefense, orbital.PosX, orbital.PosY, target, rounds, OrbitalAccuracy, OrbitalPower));
    }

    /// <summary>
    /// Shots that reached an orbital land, round by round:
    /// beams through its shield, then each round's damage on the stack whole.
    /// The stack destroyed credits the shooter by its size and gives the
    /// shooter's player an incident.
    /// </summary>
    internal void ResolveOrbitalHits(Battle battle, Orbital orbital, ref StepEvents events)
    {
        if (orbital.Dead)
        {
            return;
        }

        var s = orbital.Structure;
        var credit = s.Count * (s.Tech.Kind is TechInfo.ShipyardKind or TechInfo.TrainingBaseKind ? 5 : 25);
        foreach (var h in orbital.Incoming)
        {
            for (var round = 0; round < h.Rounds && !orbital.Dead; round++)
            {
                var full = h.RoundDamage();
                var x = h.Tracking ? full : OrbitalShield(s, full);
                if (x < full)
                {
                    ShieldFlash(battle, orbital, h);
                }

                if (x > 0)
                {
                    battle.LastHit = battle.Steps;
                    StrikeFlash(battle, orbital, h);
                    if (!HitStructure(s, x, ref events))
                    {
                        if (battle.Commanders[h.Owner] is { } shooter)
                        {
                            shooter.Kills += credit * 10;
                        }

                        if (h.Owner < Players.Count && orbital.Owner < Players.Count)
                        {
                            Incident(Players[h.Owner], Players[orbital.Owner], IncidentKind.StructureDestroyed);
                        }

                        TacticalOrbitalDies(battle, orbital);
                    }
                }

                h.Speed = 0.0;
            }

            ShotDies(battle, h, explode: false);
        }

        orbital.Incoming.Clear();
    }

    /// <summary>An orbital destroyed in a battle fought step by step: an explosion, gone from the field, its footprint freed.</summary>
    private void TacticalOrbitalDies(Battle battle, Orbital orbital)
    {
        if (orbital.Dead)
        {
            return;
        }

        OrbitalExplosion(battle, orbital);
        CarrierGone(battle, orbital);
        orbital.Dead = true;
        battle.Orbitals[orbital.Owner].Remove(orbital);
        battle.ClearFootprint(orbital.X, orbital.Y, (orbital.Size + 1) / 2);
    }

    /// <summary>An orbital's post-step: dead ships are no longer followed or ordered on; the shield regains.</summary>
    private static void PostStepOrbital(Orbital orbital)
    {
        orbital.Tracked.RemoveAll(o => o.Dead);
        if (orbital.Ordered is { Dead: true })
        {
            orbital.Ordered = null;
        }

        RegainShield(orbital);
    }

    /// <summary>
    /// An order to an orbital, by the order message's kind as
    /// it comes: 3 and 4 set its target; 5 sends a fighter at an enemy (and
    /// sets its target) or to guard a friend; 6 recalls a fighter; 7 turns
    /// its launching by itself over.
    /// </summary>
    internal void OrderOrbital(Battle battle, Orbital orbital, int kind, BattleObject? target)
    {
        switch (kind)
        {
            case 3 or 4 when target is not null:
                orbital.OrderState = kind;
                orbital.Ordered = target;
                break;
            case 5 when target is not null:
                if (Owner(target) == orbital.Owner)
                {
                    GuardWithFighter(battle, orbital, orbital.Owner, target);
                }
                else
                {
                    orbital.Ordered = target;
                    LaunchFromOrbital(battle, orbital, target);
                }

                break;
            case 6:
                RecallFighter(battle, orbital, orbital.Owner, skipReturning: false);
                break;
            case 7:
                orbital.AutoLaunch = !orbital.AutoLaunch;
                break;
            default:
                break;
        }
    }
}
