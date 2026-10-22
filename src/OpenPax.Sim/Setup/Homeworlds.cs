using OpenPax.Sim.Galaxy;
using OpenPax.Sim.Players;

namespace OpenPax.Sim.Setup;

/// <summary>Choosing each player's home star and planet; see docs/sim/players.md.</summary>
internal static class Homeworlds
{
    private const int Rounds = 10;
    private const int Candidates = 10;
    private const double MinimumHops = 2.0;
    private const int FallbackHops = 2;

    /// <summary>Picks a free star far, in wormhole jumps, from the homes already taken.</summary>
    public static int ChooseHomeStar(Game game, bool[] free, List<int> taken)
    {
        var universe = game.Universe;
        var n = universe.Stars.Count;
        var random = game.Random;
        for (var round = 0; round < Rounds; round++)
        {
            double minimum = n / game.Players.Count;
            if (minimum < MinimumHops)
            {
                minimum = MinimumHops;
            }

            for (var k = 0; k < Candidates; k++)
            {
                var c = DrawFree(random, n, free);
                if (taken.All(s => universe.Hops(s, c) >= minimum))
                {
                    return Take(c, free);
                }

                minimum = (minimum - MinimumHops) * 0.1;
                if (minimum < MinimumHops)
                {
                    minimum = MinimumHops;
                }
            }
        }

        for (var k = 0; k < Candidates; k++)
        {
            var c = DrawFree(random, n, free);
            if (taken.All(s => universe.Hops(s, c) >= FallbackHops))
            {
                return Take(c, free);
            }
        }

        return Take(DrawFree(random, n, free), free);
    }

    /// <summary>
    /// The planet whose temperature is closest by the original's measure
    /// |T - preferred + tolerance| (a bug, kept); the innermost wins ties.
    /// </summary>
    public static Planet ChooseHomePlanet(Star star, Species species)
    {
        Planet? best = null;
        var bestScore = 999999999;
        foreach (var planet in star.Planets)
        {
            var score = Math.Abs(planet.Temperature - species.PreferredTemperature + species.TemperatureTolerance);
            if (score < bestScore)
            {
                bestScore = score;
                best = planet;
            }
        }

        // A star always has at least two planets.
        return best!;
    }

    public static void MakeHomeworld(Game game, Player player, Planet planet)
    {
        planet.Type = 3;
        planet.Size = 40;
        planet.Rating = 5;
        planet.Moons = 2;

        // The original steps the orbit once more here, so a homeworld has
        // moved two steps where other planets have moved one.
        planet.Orbit.Step();

        // A homeworld is always one of the original's classes.
        planet.Kind = 0;
        planet.Breathing = Planet.BreathingLike;
        var species = player.Species;
        if (species.Has(Species.Anaerobic))
        {
            if (planet.Class == 5)
            {
                planet.Class = 3;
            }
        }
        else
        {
            for (var a = 0; a < 4; a++)
            {
                if (species.Atmospheres[a])
                {
                    planet.Class = a;
                    break;
                }
            }
        }

        planet.Temperature = species.PreferredTemperature;
        planet.Score = player.IsAi ? 11.0 : 11.0 - 2.0 * (game.Setup.Difficulty - 1);
    }

    private static int DrawFree(GameRandom random, int n, bool[] free)
    {
        int c;
        do
        {
            c = random.Next(n);
        }
        while (!free[c]);

        return c;
    }

    private static int Take(int c, bool[] free)
    {
        free[c] = false;
        return c;
    }
}
