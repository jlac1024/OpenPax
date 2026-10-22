using OpenPax.Sim.Galaxy;
using OpenPax.Sim.Players;

namespace OpenPax.Sim;

/// <summary>Questions the screens ask of the game. None of them changes anything.</summary>
public sealed partial class Game
{
    /// <summary>Ships in the fleet carrying a colonizer.</summary>
    public static int ColonyShips(Fleet fleet)
    {
        return fleet.Ships.Count(s => s.Specials.Any(c => c.Tech.Kind == TechInfo.ColonizerKind));
    }

    /// <summary>A star holds a fleet of the player with a colony ship, orbiting or not.</summary>
    public static bool CanColonizeFrom(Star star, Player player)
    {
        return star.Fleets.Any(f => f.Owner == player && ColonyShips(f) > 0);
    }

    /// <summary>The fleet is training at its owner's academy.</summary>
    public static bool InTraining(Fleet fleet)
    {
        return TrainingLevel(fleet) > 0;
    }

    /// <summary>Whether the fleet's fuel reaches a star by the shortest route.</summary>
    public bool CanReach(Fleet fleet, int toStar)
    {
        return RangeOk(fleet, Universe.Route(fleet.Star, toStar));
    }

    /// <summary>
    /// The hops of the route a fleet would take, each with whether its fuel
    /// holds there, for the map's preview. Stops at the first hop that starts
    /// at a star the owner has never seen.
    /// </summary>
    public IReadOnlyList<(int From, int To, bool Fine)> RouteHops(Fleet fleet, int toStar, Player viewer)
    {
        var route = Universe.Route(fleet.Star, toStar);
        var hops = new List<(int, int, bool)>();
        var fuel = fleet.JumpsLeft;
        for (var k = 1; k < route.Count; k++)
        {
            var (a, b) = (route[k - 1], route[k]);
            if (Universe.Stars[a].Sight[fleet.Owner.Index] == Star.Unseen)
            {
                break;
            }

            bool fine;
            if (Supplies(Universe.Stars[b], viewer))
            {
                fuel = fleet.JumpRange;
                fine = true;
            }
            else
            {
                fuel--;
                fine = fuel > 0 && Universe.Stars.Any(s => Universe.Hops(b, s.Index) <= fuel && Supplies(s, viewer));
            }

            hops.Add((a, b, fine));
        }

        return hops;
    }
}
