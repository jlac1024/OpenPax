using OpenPax.Sim;
using OpenPax.Sim.Galaxy;
using OpenPax.Sim.Players;

namespace OpenPax.Ai;

/// <summary>Where a computer player's colony ships settle and travel (+0xFD8).</summary>
internal static class ColonyShips
{
    private const int GasGiantClass = 5;

    private readonly record struct Target(double Score, Planet Planet);

    /// <summary>
    /// Every colony fleet circling a free planet settles it, whatever the
    /// planet (no gas giant or habitability test).
    /// </summary>
    public static void Colonize(Game game, Player p)
    {
        foreach (var fleet in Fleets.ColonyFleets(p))
        {
            if (fleet.OrbitCenter is Planet { Colony: null } planet && !fleet.InCombat
                && game.UseColonyShip(fleet) is { } generation)
            {
                var events = StepEvents.None;
                game.Colonize(p, planet, generation, ref events);
            }
        }
    }

    /// <summary>
    /// All colony fleets stop, then are sent to the best free planets near
    /// the empire, then anywhere, then to the best defended own colonies.
    /// </summary>
    public static void Send(Game game, Player p)
    {
        var reset = false;
        foreach (var fleet in p.Fleets.ToList())
        {
            if (Fleets.IsColonyFleet(fleet) && !fleet.Retreating)
            {
                Fleets.Stop(game, fleet);
                reset = true;
            }
        }

        if (!reset)
        {
            return;
        }

        var near = game.Universe.Stars.Select(s => Near(game, p, s)).ToArray();
        SendTo(game, p, Targets(game, p, near));
        if (SomeIdle(p))
        {
            SendTo(game, p, Targets(game, p, null));
        }

        if (SomeIdle(p))
        {
            SendByFleet(game, p);
        }
    }

    /// <summary>The star holds a colony of the player or is one wormhole from one.</summary>
    private static bool Near(Game game, Player p, Star star)
    {
        bool Holds(Star s) => s.Planets.Any(pl => pl.Owner == p);
        return Holds(star) || star.Wormholes.Any(w => w.Partner is { } other && Holds(game.Universe.Stars[other.Star]));
    }

    /// <summary>Free planets that are not gas giants, best first: max population * habitability * planet score.</summary>
    private static Target[] Targets(Game game, Player p, bool[]? near)
    {
        var targets = new List<Target>();
        foreach (var star in game.Universe.Stars)
        {
            if (near is not null && !near[star.Index])
            {
                continue;
            }

            foreach (var planet in star.Planets)
            {
                if (planet.Colony is null && planet.Class != GasGiantClass)
                {
                    var score = (double)Colony.MaximumPopulation(p, planet, null) * Colony.Habitability(p.Species, planet) * planet.Score;
                    targets.Add(new Target(score, planet));
                }
            }
        }

        var array = targets.ToArray();
        MsvcSort.Sort<Target>(array, (a, b) => a.Score > b.Score ? -1 : b.Score > a.Score ? 1 : 0);
        return array;
    }

    private static void SendTo(Game game, Player p, Target[] targets)
    {
        foreach (var target in targets)
        {
            var star = StarOf(game, target.Planet);
            foreach (var fleet in Fleets.ColonyFleets(p))
            {
                if (fleet.Orders.Count > 0 || fleet.InCombat)
                {
                    continue;
                }

                if (Fleets.SendSafely(game, fleet, target.Planet, star))
                {
                    break;
                }
            }
        }
    }

    /// <summary>Left-over colony ships go to the best defended own colony each can reach.</summary>
    private static void SendByFleet(Game game, Player p)
    {
        var list = p.Colonies.Select(c => new Target(Fleets.FriendlyStrength(game, p, c.Planet), c.Planet)).ToArray();
        MsvcSort.Sort<Target>(list, (a, b) => a.Score > b.Score ? -1 : b.Score > a.Score ? 1 : 0);
        foreach (var fleet in Fleets.ColonyFleets(p))
        {
            if (fleet.Orders.Count > 0 || fleet.InCombat)
            {
                continue;
            }

            foreach (var target in list)
            {
                if (Fleets.SendSafely(game, fleet, target.Planet, StarOf(game, target.Planet)))
                {
                    break;
                }
            }
        }
    }

    private static bool SomeIdle(Player p)
    {
        return Fleets.ColonyFleets(p).Any(f => f.Orders.Count == 0 && !f.InCombat);
    }

    private static int StarOf(Game game, Planet planet)
    {
        return game.Universe.Stars.First(s => s.Planets.Contains(planet)).Index;
    }
}
