using OpenPax.Sim;
using OpenPax.Sim.Galaxy;
using OpenPax.Sim.Players;

namespace OpenPax.Ai;

/// <summary>What the computer player modules ask about fleets, ships and places.</summary>
internal static class Fleets
{
    public static bool HasColonizer(Design? ship)
    {
        return ship is not null && ship.Specials.Any(c => c.Tech.Kind == TechInfo.ColonizerKind);
    }

    public static bool IsColonyFleet(Fleet fleet)
    {
        return fleet.Ships.Any(HasColonizer);
    }

    /// <summary>Colony fleets with ships, in the owner's fleet order.</summary>
    public static IEnumerable<Fleet> ColonyFleets(Player p)
    {
        return p.Fleets.Where(f => IsColonyFleet(f) && f.Ships.Count > 0).ToList();
    }

    /// <summary>Colony ships at the head of yard queues, plus every ship in colony fleets.</summary>
    public static int ColonyShipCount(Player p)
    {
        var n = p.Colonies.Count(c => c.FirstOfKind(TechInfo.ShipyardKind) is { Queue.Count: > 0 } yard && HasColonizer(yard.Queue[0]));
        foreach (var f in p.Fleets)
        {
            if (IsColonyFleet(f))
            {
                n += f.Ships.Count;
            }
        }

        return n;
    }

    public static bool AtWar(Game game, Player p)
    {
        return game.Players.Any(o => o != p && p.Treaties[o.Index].Level() < 1);
    }

    /// <summary>A ship's or design's upkeep: its cost, times 0.8 for nomads.</summary>
    public static double Upkeep(Player p, Design? design)
    {
        if (design is null)
        {
            return 0.0;
        }

        var cost = (double)design.Cost;
        return p.Species.Is(Species.Nomad) ? cost * 0.8 : cost;
    }

    /// <summary>Some fleet of another player at war with this one is in the star.</summary>
    public static bool HostileHere(Player p, Star star)
    {
        return star.Fleets.Any(f => f.Owner != p && p.Treaties[f.Owner.Index].Level() < 1);
    }

    /// <summary>Whether any stop on a fleet's route has a hostile fleet (the far end of each wormhole).</summary>
    public static bool Danger(Game game, Fleet fleet)
    {
        foreach (var o in fleet.Orders)
        {
            var star = o.Target switch
            {
                Wormhole w when w.Partner is { } partner => partner.Star,
                Wormhole w => w.Star,
                Planet or Fleet => StarOf(game, o.Target),
                _ => -1,
            };
            if (star >= 0 && HostileHere(fleet.Owner, game.Universe.Stars[star]))
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// Colony and idle orbiting fleet strength at a planet of the player and
    /// friends. Quirk kept: players never met count as friends.
    /// </summary>
    public static double FriendlyStrength(Game game, Player p, Planet planet)
    {
        bool Friendly(Player o) => o == p || p.Treaties[o.Index].Level() > 3;

        var sum = 0.0;
        if (planet.Colony is { } colony && Friendly(colony.Owner))
        {
            sum = game.ColonyStrength(colony);
        }

        var star = game.Universe.Stars.First(s => s.Planets.Contains(planet));
        foreach (var f in star.Fleets)
        {
            if (f.OrbitCenter == planet && f.Orders.Count == 0 && Friendly(f.Owner))
            {
                sum = f.Strength + sum;
            }
        }

        return sum;
    }

    /// <summary>
    /// Sends a fleet to a place in its star or along a route in range. A
    /// route through a hostile fleet is called off (the fleet stops and
    /// draws a new orbit). True when the fleet is on its way.
    /// </summary>
    public static bool SendSafely(Game game, Fleet fleet, ISpaceObject place, int star)
    {
        if (fleet.Star == star)
        {
            game.GiveOrder(fleet, Order.To(place));
            return true;
        }

        if (!game.PlanRoute(fleet, star))
        {
            return false;
        }

        game.GiveOrder(fleet, Order.To(place));
        if (!Danger(game, fleet))
        {
            return true;
        }

        Stop(game, fleet);
        return false;
    }

    /// <summary>The star a planet, wormhole or fleet is in (+0x78).</summary>
    public static int StarOf(Game game, ISpaceObject place)
    {
        return place switch
        {
            Fleet f => f.Star,
            Wormhole w => w.Star,
            _ => game.Universe.Stars.First(s => s.Planets.Contains((Planet)place)).Index,
        };
    }

    /// <summary>Stops a fleet: orders dropped, sitting still (an orbit is drawn).</summary>
    public static void Stop(Game game, Fleet fleet)
    {
        game.ClearOrders(fleet);
        fleet.EnterOrbit(game.Random, null);
    }
}
