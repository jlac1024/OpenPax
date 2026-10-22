using OpenPax.Sim.Combat;
using OpenPax.Sim.Players;

namespace OpenPax.Sim;

/// <summary>
/// How a side fights by itself (docs/sim/combat-step.md): a computer side,
/// or a human side with AUTO on. The same think serves the battle resolved
/// at once, where it is always forced and so never rolls for retreat.
/// </summary>
public sealed partial class Game
{
    private const int ThinksBeforeActing = 7;

    /// <summary>Added to the retreat threshold by experience level (the table).</summary>
    private static readonly int[] _retreatBias = [-30, -10, 0, 10, 25, 50, 100];

    /// <summary>Each side notes its ships when the battle begins; the first ship added counts for its strength.</summary>
    private void PrimeCommanders(Battle battle)
    {
        for (var p = 0; p < Battle.Players; p++)
        {
            if (battle.Commanders[p] is not { } c)
            {
                continue;
            }

            foreach (var ship in battle.Ships[p])
            {
                c.StartMaxStructure += ship.Ship.MaxStructure;
                c.StartStructure += ship.Ship.Structure;
                c.StartStrength = ship.Ship.Strength(Tables.Tech);
            }

            c.StructureNow = c.StartStructure;
            c.Strength = c.StartStrength;
            c.Kills = 0;
        }
    }

    /// <summary>A side notes its ships as they are now.</summary>
    private void Tally(Battle battle, Commander c)
    {
        c.StructureNow = 0;
        c.Strength = 0;
        foreach (var ship in battle.Ships[c.Player])
        {
            c.StructureNow += ship.Ship.Structure;
            c.Strength = ship.Ship.Strength(Tables.Tech);
        }
    }

    /// <summary>
    /// How readily a side's ships hold: what is left of its own
    /// first ship against what is left of its enemies', in thousandths, plus
    /// 40 (140 for a warrior species). An enemy that never thinks always
    /// looks whole. Quirk kept: with no strength of its own at the start its
    /// share counts as 1, not 1000.
    /// </summary>
    private int RetreatThreshold(Battle battle, Commander c)
    {
        var p = c.Player;
        int s1 = 0, s2 = 0;
        for (var q = 0; q < Battle.Players; q++)
        {
            if (q != p && AtWar(p, q) && battle.Commanders[q] is { } enemy)
            {
                s1 += enemy.StartStrength;
                s2 += enemy.Strength;
            }
        }

        var e = s1 < 1 ? 1 : s2 * 1000 / s1;

        // Port decision (docs/decisions.md): the original divides by zero
        // and crashes when the enemies are worn below a thousandth; the port
        // counts them as one thousandth, which means no retreat.
        if (e == 0)
        {
            e = 1;
        }

        var o = c.StartStrength < 1 ? 1 : c.Strength * 1000 / c.StartStrength;
        return o * 100 / e + (Players[p].Species.Is(Species.Warrior) ? 140 : 40);
    }

    /// <summary>
    /// Whether a ship breaks off: the threshold shifted by its
    /// experience; at 100 or more it holds with no roll, else it goes when a
    /// roll of 100 beats it.
    /// </summary>
    private bool RollsRetreat(BattleShip ship, int threshold)
    {
        var t = threshold + _retreatBias[Math.Clamp(ship.Ship.ExperienceLevel, 0, _retreatBias.Length - 1)];
        return t <= 99 && t < Random.Next(100);
    }

    /// <summary>
    /// A computer side gives up when no enemy ship or orbital is
    /// left, unless it is the planet's owner or can bomb an enemy colony:
    /// every ship not intercepting retreats. A human side never gives up.
    /// </summary>
    private bool Disengage(Battle battle, Commander c)
    {
        var p = c.Player;
        if (!Players[p].IsAi)
        {
            return false;
        }

        var none = !Enumerable.Range(0, Battle.Players)
            .Any(q => q != p && AtWar(p, q) && (battle.Ships[q].Count > 0 || battle.Orbitals[q].Count > 0));
        var q = PlanetOwner(battle);
        if (q == p)
        {
            none = false;
        }

        if (none && q >= 0 && AtWar(p, q) && battle.Ships[p].Any(s => s.Ship.CanAttackPlanets))
        {
            none = false;
        }

        if (none)
        {
            foreach (var ship in battle.Ships[p].ToList())
            {
                if (!ship.Fleet.Intercepting)
                {
                    PickRetreat(battle, ship);
                }
            }
        }

        c.Done = none;
        return none;
    }

    /// <summary>
    /// A side that fights by itself thinks: nothing on its first
    /// seven thinks; then, unless it gives up, on every other think each ship
    /// of a side that is not the planet's owner may break off, and each idle
    /// ship takes the best target (colony ships only face it).
    /// </summary>
    private void Think(Battle battle, Commander c)
    {
        if (!c.Computer)
        {
            return;
        }

        var old = c.Counter++;
        if (old < ThinksBeforeActing)
        {
            return;
        }

        var p = c.Player;
        // The AUTO button's color follows a human side's commander (with one human, the original's local player).
        if (!Players[p].IsAi)
        {
            battle.AutoEngaged = c.Computer;
        }

        if (battle.Ships[p].Count == 0 || c.Done || Disengage(battle, c))
        {
            return;
        }

        Tally(battle, c);
        var threshold = RetreatThreshold(battle, c);
        var owner = PlanetOwner(battle);
        foreach (var ship in battle.Ships[p].ToList())
        {
            if (c.Counter % 2 == 1 && owner != p && ship.State != BattleShip.Leaving && Players[p].IsAi && RollsRetreat(ship, threshold))
            {
                PickRetreat(battle, ship);
            }

            if (ship.State == BattleShip.Idle && PickTarget(battle, p, ship) is { } target)
            {
                var colonyShip = ship.Ship.Specials.Any(s => s.Tech.Kind == TechInfo.ColonizerKind);
                OrderShip(battle, ship, colonyShip ? BattleOrderKind.Target : BattleOrderKind.Attack, null, target);
            }
        }
    }

    /// <summary>A side is made to think at once: never on a retreat roll.</summary>
    private void ForceThink(Battle battle, Commander c)
    {
        c.Counter = ThinksBeforeActing;
        Think(battle, c);
    }

    /// <summary>In sub-steps 0 to 15 the side of that number thinks.</summary>
    private void ThinkStep(Battle battle)
    {
        if (battle.SubStep < ThinkingSubSteps && battle.Commanders[battle.SubStep] is { } c)
        {
            Think(battle, c);
        }
    }
}
