using OpenPax.Sim.Galaxy;
using OpenPax.Sim.Players;

namespace OpenPax.Sim;

/// <summary>
/// Orders a player gives through the screens. The original makes each a
/// command object run in the next command pass (single player) or sent to
/// the host; here the screens call these between ticks.
/// </summary>
public sealed partial class Game
{
    /// <summary>
    /// A fleet order: with replace, the orders so far are removed one by one
    /// (so a fleet set to colonize may found its colony where it is); then
    /// move to the target, toggle intercepting, or go and colonize or attack.
    /// </summary>
    public void FleetOrder(Fleet fleet, FleetOrderKind kind, Order? order, bool replace)
    {
        if (replace)
        {
            ClearOrders(fleet);
        }

        switch (kind)
        {
            case FleetOrderKind.ToggleIntercept:
                fleet.Intercepting = !fleet.Intercepting;
                break;
            case FleetOrderKind.Move when order is not null:
                GiveOrder(fleet, order);
                break;
            case FleetOrderKind.Colonize or FleetOrderKind.Attack when order is not null:
                GiveOrder(fleet, order);

                fleet.Mode = kind == FleetOrderKind.Colonize ? FleetMode.Colonize : FleetMode.Attack;
                break;
        }
    }

    /// <summary>
    /// A fleet order to another star: refused when the fleet cannot reach
    /// it; otherwise an order of the kind for each wormhole on the way,
    /// replacing the old orders on the first only. Quirk kept: an attack or
    /// colonize kind is given for every hop, so the fleet travels in that mode.
    /// </summary>
    public bool FleetRoute(Fleet fleet, int toStar, FleetOrderKind kind, bool replace, IReadOnlyCollection<int>? splitOff = null)
    {
        var route = Universe.Route(fleet.Star, toStar);
        if (!RangeOk(fleet, route))
        {
            return false;
        }

        // The ships left out go their own way first (the split command before the first hop's order).
        if (splitOff is { Count: > 0 })
        {
            SplitFleet(fleet, splitOff);
        }

        for (var k = 1; k < route.Count && k <= 100; k++)
        {
            var from = Universe.Stars[route[k - 1]];
            var hole = from.Wormholes.First(w => w.Partner?.Star == route[k]);
            FleetOrder(fleet, kind, Order.To(hole), replace && k == 1);
        }

        return true;
    }

    /// <summary>
    /// The split command: the ships named leave the fleet for a
    /// new one beside it, which takes the fleet's place, mode and intercept
    /// setting; the fleet keeps the rest and its
    /// orders. No draws.
    /// </summary>
    public Fleet? SplitFleet(Fleet fleet, IReadOnlyCollection<int> shipIds)
    {
        var owner = fleet.Owner;
        if (!owner.Fleets.Contains(fleet) || !fleet.Ships.Any(s => shipIds.Contains(s.ShipId)))
        {
            return null;
        }

        var star = Universe.Stars[fleet.Star];
        owner.FleetCounter++;
        var copy = new Fleet(ObjectIds.Take(), star.Name + Tables.FleetNameText + owner.FleetCounter, owner, star.Index)
        {
            Mode = fleet.Mode,
            Intercepting = fleet.Intercepting,
        };
        copy.TakePlaceOf(fleet);
        copy.UpdateStats(Tables.Tech);

        // the system takes it at the front, supplies it, and sight is worked out.
        star.Fleets.Insert(0, copy);
        if (Supplies(star, owner))
        {
            Resupply(copy);
        }

        UpdateSight(star);
        owner.Fleets.Insert(0, copy);
        foreach (var ship in fleet.Ships.ToList())
        {
            if (shipIds.Contains(ship.ShipId))
            {
                fleet.RemoveShip(ship, Tables.Tech);
                copy.AddShip(ship, Tables.Tech);
            }
        }

        return copy;
    }

    /// <summary>
    /// The "c" key: a colony at once on a planet with none. Quirk kept: it
    /// does not check that the fleet still has a colony ship (the colony is
    /// then founded as generation 1) or still orbits the planet.
    /// </summary>
    public StepEvents ColonizeNow(Player player, Planet planet, Fleet fleet)
    {
        var events = StepEvents.None;
        // The fix: only a fleet at the planet with a colony ship can.
        if (Fixed(Fixes.ColonizeNeedsShip) && (fleet.OrbitCenter != planet || fleet.Owner != player || !HasColonyShip(fleet)))
        {
            return events;
        }

        if (planet.Colony is null)
        {
            var generation = UseColonyShip(fleet) ?? -1;
            Colonize(player, planet, generation, ref events);
        }

        return events;
    }
}
