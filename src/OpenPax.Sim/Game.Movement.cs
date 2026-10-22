using OpenPax.Sim.Galaxy;
using OpenPax.Sim.Numerics;
using OpenPax.Sim.Players;

namespace OpenPax.Sim;

/// <summary>Fleet orders, movement and arrival (docs/sim/movement.md).</summary>
public sealed partial class Game
{
    private const double BoostFactor = 10.0;
    private const double PlanetStopDistance = 30.0;
    private const double FriendlyFleetStopDistance = 5.0;
    private const double FleetStopDistance = 50.0;
    private const int GasGiantClass = 5;

    /// <summary>
    /// Gives a fleet an order. The first order takes the fleet out of its
    /// orbit; a later one goes on the end unless it leads to the same place
    /// as the last.
    /// </summary>
    public void GiveOrder(Fleet fleet, Order order)
    {
        // Any refit waiting for the fleet's ships is called off.
        foreach (var ship in fleet.Ships)
        {
            ship.Refit = null;
        }

        if (fleet.Orders.Count == 0)
        {
            fleet.Orders.Add(order);
            fleet.LeaveOrbit();
            return;
        }

        var last = fleet.Orders[^1];
        if (last.Position == order.Position)
        {
            return;
        }

        if (last.Target is { } a && order.Target is { } b && a == b)
        {
            return;
        }

        fleet.Orders.Add(order);
    }

    /// <summary>What the fleet does at its last target.</summary>
    public void SetMode(Fleet fleet, FleetMode mode)
    {
        fleet.Mode = mode;
    }

    private void MoveFleet(Fleet fleet, ref StepEvents events)
    {
        double budget;
        if (fleet.BoostTicks < 1)
        {
            fleet.Retreating = false;
            budget = fleet.Speed;
        }
        else
        {
            budget = fleet.Speed * BoostFactor;
            fleet.BoostTicks--;
        }

        while (budget > 0.0)
        {
            if (fleet.Orders.Count == 0)
            {
                fleet.OrbitStep();
                budget = 0.0;
                continue;
            }

            var order = fleet.Orders[0];
            var (tx, ty) = order.Position;
            var degree = X87.ToInt(Direction(fleet.X, fleet.Y, tx, ty));
            var dist = Distance(tx, ty, fleet.X, fleet.Y);
            if (fleet.Orders.Count == 1 && order.Target is { } target)
            {
                dist -= StopDistance(fleet, target);
                if (dist < 0.0)
                {
                    dist = 0.0;
                }
            }

            var step = dist <= budget ? dist : budget;
            budget -= step;
            var x = X87.ToInt(Tables.Cos[degree] * step + fleet.X - -0.5);
            var y = X87.ToInt(Tables.Sin[degree] * step + fleet.Y - -0.5);
            fleet.MoveTo(x, y);
            if (step == dist)
            {
                Arrive(fleet, ref events);
            }
        }
    }

    /// <summary>How far short of its last target a fleet stops.</summary>
    private static double StopDistance(Fleet fleet, ISpaceObject target)
    {
        switch (target)
        {
            case Planet or StarCenter:
                return PlanetStopDistance;
            case Fleet other:
                var friendly = other.Owner == fleet.Owner || fleet.Owner.Treaties[other.Owner.Index].Level() > 1;
                return friendly && fleet.Mode != FleetMode.Attack ? FriendlyFleetStopDistance : FleetStopDistance;
            default:
                return 0.0;
        }
    }

    private void Arrive(Fleet fleet, ref StepEvents events)
    {
        var order = fleet.Orders[0];
        var target = order.Target;
        if (fleet.Orders.Count > 1)
        {
            if (target is Wormhole hole && !ArriveAtWormhole(fleet, hole))
            {
                return;
            }

            RemoveOrder(fleet, order);
            return;
        }

        switch (target)
        {
            case null:
                break;
            case Fleet other when other.Owner == fleet.Owner:
                if (MergeInto(fleet, other))
                {
                    return;
                }

                break;
            case Fleet other:
                if (fleet.Ships.Count > 0 && other.Ships.Count > 0 && HostileTo(fleet, other.Owner, ref events)
                    && !other.Retreating && !fleet.InCombat)
                {
                    if (!other.InCombat)
                    {
                        Incident(fleet.Owner, other.Owner, IncidentKind.FleetAttacked);
                        ReactToAttack(fleet.Owner, other.Owner);
                        if (other.OrbitCenter is Planet { Colony: not null } at)
                        {
                            StartPlanetBattle(fleet, at, ref events);
                        }
                        else
                        {
                            StartFleetBattle(fleet, other);
                        }
                    }
                }

                break;
            case Planet planet:
                if (PlanetHostile(fleet, planet, ref events) && !fleet.InCombat && !planet.InCombat)
                {
                    Incident(fleet.Owner, planet.Owner!, IncidentKind.PlanetAttacked); // hostile means it has an owner
                    ReactToAttack(fleet.Owner, planet.Owner!);

                    StartPlanetBattle(fleet, planet, ref events);
                    RemoveOrder(fleet, order);
                    return;
                }

                OrbitAt(fleet, planet);
                break;
            case StarCenter center:
                // The fourth kind: orbited as a planet is.
                OrbitAt(fleet, center);
                break;
            case Wormhole hole:
                if (!ArriveAtWormhole(fleet, hole))
                {
                    return;
                }

                break;
            default:
                return;
        }

        RemoveOrder(fleet, order);
    }

    /// <summary>
    /// Jumps when the fleet has jumps left. Otherwise it stops at the
    /// wormhole and drops every order: a human's fleet circles the
    /// wormhole, a computer player's sits on it. False when orders were dropped.
    /// </summary>
    private bool ArriveAtWormhole(Fleet fleet, Wormhole hole)
    {
        if (fleet.JumpsLeft > 0)
        {
            Jump(fleet, hole);
            return true;
        }

        fleet.MoveTo(hole.X, hole.Y);
        if (fleet.Owner.IsAi)
        {
            fleet.EnterOrbit(Random, null);
            fleet.OrbitAngle = Direction(hole.X, hole.Y, fleet.X, fleet.Y);
        }
        else
        {
            OrbitAt(fleet, hole);
        }

        ClearOrders(fleet);
        return false;
    }

    /// <summary>Ships go over, newest first, until the target is full. True if this fleet ran out.</summary>
    private bool MergeInto(Fleet fleet, Fleet target)
    {
        var room = Fleet.MaxShips - target.Ships.Count;
        while (room > 0 && fleet.Ships.Count > 0)
        {
            var ship = fleet.Ships[0];
            fleet.RemoveShip(ship, Tables.Tech);
            target.AddShip(ship, Tables.Tech);
            room--;
            if (fleet.Ships.Count == 0)
            {
                RemoveOrder(fleet, fleet.Orders[0]);
                return true;
            }
        }

        return false;
    }

    /// <summary>An attacking fleet declares war; otherwise a treaty level below 2 is hostile.</summary>
    private bool HostileTo(Fleet fleet, Player other, ref StepEvents events)
    {
        if (fleet.Mode == FleetMode.Attack)
        {
            SetTreaty(fleet.Owner, other, Treaty.War, Treaty.InForce, ref events);
            SetTreaty(other, fleet.Owner, Treaty.War, Treaty.InForce, ref events);
            return true;
        }

        return fleet.Owner.Treaties[other.Index].Level() < 2;
    }

    /// <summary>Whether arriving at a planet means attacking it; the rules differ for computer and human fleets.</summary>
    private bool PlanetHostile(Fleet fleet, Planet planet, ref StepEvents events)
    {
        var owner = planet.Owner;
        if (owner is null || owner == fleet.Owner)
        {
            return false;
        }

        var level = fleet.Owner.Treaties[owner.Index].Level();
        if (fleet.Owner.IsAi)
        {
            // a colony fleet  never attacks.
            return level <= 1 && !fleet.Ships.Any(s => s.Specials.Any(c => c.Tech.Kind == TechInfo.ColonizerKind));
        }

        if (level > 1)
        {
            if (fleet.Mode != FleetMode.Attack)
            {
                return false;
            }

            SetTreaty(fleet.Owner, owner, Treaty.War, Treaty.InForce, ref events);
            SetTreaty(owner, fleet.Owner, Treaty.War, Treaty.InForce, ref events);
        }

        return true;
    }

    /// <summary>Enters orbit around an object at the fleet radius, facing away from it.</summary>
    private void OrbitAt(Fleet fleet, ISpaceObject center)
    {
        fleet.EnterOrbit(Random, center);
        fleet.OrbitAngle = Direction(center.X, center.Y, fleet.X, fleet.Y);
    }

    /// <summary>Through a wormhole to its partner's system: every ship uses a jump.</summary>
    private void Jump(Fleet fleet, Wormhole hole)
    {
        if (hole.Partner is not { } partner)
        {
            return;
        }

        foreach (var ship in fleet.Ships)
        {
            ship.JumpsSinceSupply++;
        }

        fleet.UpdateStats(Tables.Tech);
        fleet.MoveTo(partner.X, partner.Y);
        OrbitAt(fleet, partner);

        var departure = Universe.Stars[fleet.Star];
        departure.Fleets.Remove(fleet);
        UpdateSight(departure);
        fleet.Star = partner.Star;
        var arrival = Universe.Stars[partner.Star];
        arrival.Fleets.Insert(0, fleet);
        if (Supplies(arrival, fleet.Owner))
        {
            Resupply(fleet);
        }

        UpdateSight(arrival);
        hole.Known = true;
        partner.Known = true;
    }

    /// <summary>
    /// Takes an order off the queue. When the queue empties the fleet's
    /// mode goes back to none.
    /// </summary>
    private void RemoveOrder(Fleet fleet, Order order)
    {
        fleet.Orders.Remove(order);
        if (fleet.Orders.Count != 0)
        {
            return;
        }

        if (fleet.Mode == FleetMode.Colonize && fleet.OrbitCenter is Planet { Colony: null } planet
            && planet.Class != GasGiantClass && UseColonyShip(fleet) is { } generation)
        {
            var events = StepEvents.None;
            Colonize(fleet.Owner, planet, generation, ref events);
        }

        // Callers on the arrival path also pass a flag for the owner's
        // notice (virtual +0x4C), which does nothing for either player class.
        fleet.Mode = FleetMode.None;
    }

    /// <summary>Whether the fleet has a ship that can found a colony.</summary>
    internal static bool HasColonyShip(Fleet fleet) => fleet.Ships.Any(s => s.Specials.Any(c => c.Tech.Kind == TechInfo.ColonizerKind));

    /// <summary>
    /// The newest ship with a colonizer leaves the fleet for good; returns
    /// that colonizer's generation (its place among the colonizers, from 1).
    /// </summary>
    internal int? UseColonyShip(Fleet fleet)
    {
        foreach (var ship in fleet.Ships)
        {
            if (ship.Specials.FirstOrDefault(c => c.Tech.Kind == TechInfo.ColonizerKind) is { } colonizer)
            {
                var generation = Tables.IndexInKind(Tables.Tech(colonizer.Tech.LookId)) + 1;
                fleet.RemoveShip(ship, Tables.Tech);
                return generation;
            }
        }

        return null;
    }

    internal void ClearOrders(Fleet fleet)
    {
        while (fleet.Orders.Count > 0)
        {
            RemoveOrder(fleet, fleet.Orders[0]);
        }
    }

    internal static double Direction(int fromX, int fromY, int toX, int toY)
    {
        return X87.DirectionDegrees(toX - fromX, toY - fromY);
    }

    internal static double Distance(int ax, int ay, int bx, int by)
    {
        var dx = ax - bx;
        var dy = ay - by;
        return Math.Sqrt(dx * dx + dy * dy);
    }
}
