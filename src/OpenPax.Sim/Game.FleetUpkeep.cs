using OpenPax.Sim.Galaxy;
using OpenPax.Sim.Numerics;
using OpenPax.Sim.Players;

namespace OpenPax.Sim;

/// <summary>Fleet repair, training and decay each economy period (docs/sim/economy.md).</summary>
public sealed partial class Game
{
    // Repair bonus by experience level 0 to 6, in percent.
    private static readonly double[] _repairByLevel = [-30, -10, 0, 10, 25, 50, 100];

    private const double SetupRepair = 9999999.0;

    internal void FleetUpkeep(Fleet fleet, bool all)
    {
        var training = TrainingLevel(fleet);
        if (fleet.Owner.Support >= 100.0 || all)
        {
            var ships = 0.0;
            var repair = 0.0;
            foreach (var ship in fleet.Ships)
            {
                Train(ship, training);
                ships = ships - -1.0;
                repair = RepairFigure(ship) + repair;
            }

            if (fleet.Orders.Count == 0 && fleet.OrbitCenter is Planet { Colony: { } colony }
                && colony.FirstOfKind(TechInfo.ShipyardKind) is { } yard
                && (colony.Owner == fleet.Owner || fleet.Owner.Treaties[colony.Owner.Index].Has(3)))
            {
                repair = ((double)yard.Count - -4.0) * repair;
            }

            foreach (var ship in fleet.Ships)
            {
                RepairShip(ship, all ? SetupRepair : repair / ships);
            }
        }
        else
        {
            var risk = 100.0 - fleet.Owner.Support;
            foreach (var ship in fleet.Ships.ToList())
            {
                Train(ship, training);
                if (Random.Next(100) + 1 <= risk)
                {
                    ship.Structure -= (ship.Type + 1) * 10;
                    if (ship.Structure <= 0)
                    {
                        fleet.RemoveShip(ship, Tables.Tech);
                    }
                }
            }
        }

        fleet.UpdateStats(Tables.Tech);
    }

    /// <summary>
    /// A fleet circling its owner's planet with a fully built kind 6
    /// structure trains ships below level 2; the figure grows with the stack.
    /// </summary>
    private static int TrainingLevel(Fleet fleet)
    {
        if (fleet.OrbitCenter is not Planet { Colony: { } colony } || colony.Owner != fleet.Owner)
        {
            return 0;
        }

        if (colony.FirstOfKind(TechInfo.TrainingBaseKind) is not { Percent: 100.0 } trainer
            || !fleet.Ships.Any(s => s.ExperienceLevel < 2))
        {
            return 0;
        }

        return X87.ToInt((trainer.Count - -2.0) * trainer.Percent * 0.01);
    }

    private static void Train(Design ship, int training)
    {
        if (training != 0 && ship.ExperienceLevel < 2)
        {
            ship.AddExperience(training);
        }
    }

    /// <summary>(1 + level bonus) * (hull + 1) * 10, times 1.2 for nomads.</summary>
    // the original forms  * 10 first
    private static double RepairFigure(Design ship)
    {
        var figure = (_repairByLevel[ship.ExperienceLevel] * 0.01 - -1.0) * (((double)ship.Type - -1.0) * 10.0);
        return ship.Owner?.Species.Is(Species.Nomad) == true ? figure * 1.2 : figure;
    }

    /// <summary>Half the repair goes to structure, the rest to damaged components in slot order.</summary>
    private static void RepairShip(Design ship, double amount)
    {
        var rest = amount;
        if (ship.Structure < ship.MaxStructure)
        {
            var hull = Math.Min(amount * 0.5, (double)(ship.MaxStructure - ship.Structure));
            rest = amount - hull;
            ship.Structure = Math.Min(ship.MaxStructure, ship.Structure + X87.ToInt(hull));
        }

        foreach (var c in ship.Components)
        {
            if (rest == 0.0)
            {
                break;
            }

            if (c.Condition < 100)
            {
                var used = Math.Min(X87.ToInt(rest), c.Damage);
                c.Damage -= used;
                rest -= used;
            }
        }
    }
}
