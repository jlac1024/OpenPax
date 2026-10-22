using OpenPax.Sim.Galaxy;
using OpenPax.Sim.Players;

namespace OpenPax.Sim;

/// <summary>Shipyards building ships, and making fleets (docs/sim/economy.md).</summary>
public sealed partial class Game
{
    /// <summary>
    /// A new empty fleet at a planet: a human's circles it, a computer
    /// player's sits on its position. Both draw an orbit.
    /// </summary>
    internal Fleet CreateFleet(Player player, Star star, Planet planet)
    {
        var id = ObjectIds.Take();
        player.FleetCounter++;
        var name = star.Name + Tables.FleetNameText + player.FleetCounter;
        var fleet = new Fleet(id, name, player, star.Index);
        if (player.IsAi)
        {
            fleet.MoveTo(planet.X, planet.Y);
            fleet.EnterOrbit(Random, null);
        }
        else
        {
            fleet.EnterOrbit(Random, planet);
        }

        fleet.UpdateStats(Tables.Tech);

        // Both the system's list and the owner's take new fleets at the front.
        star.Fleets.Insert(0, fleet);
        if (Supplies(star, player))
        {
            Resupply(fleet);
        }

        UpdateSight(star);

        player.Fleets.Insert(0, fleet);
        return fleet;
    }

    /// <summary>A ship built to a design, with its own id.</summary>
    internal Design NewShip(Design design)
    {
        var ship = design.Copy();
        ship.ShipId = SecondaryIds.Take();
        return ship;
    }

    /// <summary>Puts a ship at the front of a shipyard's queue; work starts again on it.</summary>
    internal static void QueueShip(Structure yard, Design ship)
    {
        yard.Queue.Insert(0, ship);
        yard.Progress = 0.0;
        yard.Total = ship.BuildCost();
    }

    /// <summary>The colony's first shipyard builds, if it is whole and upkeep is paid.</summary>
    private double BuildShips(Colony colony, double points)
    {
        if (colony.FirstOfKind(TechInfo.ShipyardKind) is { } yard && points > 0.0
            && colony.Owner.Support >= 100.0 && yard.Percent >= 100.0)
        {
            points = BuildAtYard(yard, points);
        }

        return points;
    }

    private double BuildAtYard(Structure yard, double points)
    {
        if (yard.Queue.Count == 0)
        {
            StartRefit(yard);
            if (yard.Queue.Count == 0)
            {
                return points;
            }
        }

        if (yard.Progress >= 0.0 && yard.Progress < yard.Total)
        {
            var work = YardWork(yard, points);
            var finished = false;
            if (work > 0.0)
            {
                var next = work + yard.Progress;
                if (yard.Total < next)
                {
                    // Quirk kept: the leftover is in progress units, not construction.
                    // The fix: the leftover share of the work, as construction.
                    points = Fixed(Fixes.ShipyardOverflow)
                        ? points * (work - (yard.Total - yard.Progress)) / work
                        : work - (yard.Total - yard.Progress);
                    yard.Progress = yard.Total;
                    finished = true;
                }
                else
                {
                    yard.Progress = next;
                }
            }

            if (!finished)
            {
                points = 0.0;
            }
        }

        if (yard.Total <= yard.Progress)
        {
            FinishShip(yard);
        }

        return points;
    }

    /// <summary>((count - 1) * 0.5 + 1) * condition, times the first shipyard advisor's skill + 1.</summary>
    public static double YardWork(Structure yard, double points)
    {
        var rate = ((yard.Count - 1) * 0.5 - -1.0) * yard.Percent * 0.01;
        var advisor = yard.Colony.Owner.Advisors.FirstOrDefault(a => a.Role == 3 && a.Star == Advisor.NoStar);
        return advisor is null ? rate * points : (advisor.Value(3) - -1.0) * rate * points;
    }

    /// <summary>
    /// With nothing queued, the first ship marked for refit in an idle fleet
    /// circling the yard's planet goes in; work already in the old ship counts.
    /// </summary>
    private void StartRefit(Structure yard)
    {
        var planet = yard.Colony.Planet;
        var star = Universe.Stars[yard.Colony.Star];
        foreach (var fleet in star.Fleets)
        {
            if (fleet.Owner != yard.Colony.Owner || fleet.Orders.Count != 0 || fleet.OrbitCenter != planet)
            {
                continue;
            }

            if (fleet.Ships.FirstOrDefault(s => s.Refit is not null) is not { } old)
            {
                continue;
            }

            var refit = old.Refit!; // just tested
            old.Refit = null;
            refit.Refit = old;
            fleet.RemoveShip(old, Tables.Tech);
            refit.TakeOver(old);
            QueueShip(yard, refit);
            var cost = refit.BuildCost();
            var worth = (double)old.ConditionPercent() * 0.01 * old.BuildCost();
            yard.Progress = yard.Total * (cost <= worth ? 1.0 : worth / cost);
            return;
        }
    }

    /// <summary>The head of the queue joins a fleet at the yard's planet.</summary>
    private void FinishShip(Structure yard)
    {
        var fleet = ShipyardFleet(yard.Colony);

        var ship = yard.Queue[0];
        yard.Queue.RemoveAt(0);
        yard.Progress = 0.0;
        yard.Total = yard.Queue.Count > 0 ? yard.Queue[0].BuildCost() : 0.0;
        fleet.AddShip(ship, Tables.Tech);

        Notify(new GameNotice(NoticeKind.ShipFinished, yard.Colony.Owner, Place: yard.Colony.Planet, Ship: ship));
        Happen(new ModEvent(ModEventKind.ShipBuilt, yard.Colony.Owner, Place: yard.Colony.Planet, Ship: ship));

        // A refit's old ship is gone for good.
        ship.Refit = null;
    }
}
