using OpenPax.Sim.Players;

namespace OpenPax.Sim.Setup;

/// <summary>Starting attitudes between players: one pass over all players, then a second.</summary>
internal static class Relations
{
    private static readonly int[] _attitudeByDraw = [-3, -2, -1, 1, 2, 3];

    public static void Set(Game game)
    {
        foreach (var p in game.Players)
        {
            SetAttitudes(game, p);
        }

        foreach (var p in game.Players)
        {
            SetScores(game, p);
        }
    }

    private static void SetAttitudes(Game game, Player p)
    {
        var d = game.Setup.Difficulty;
        for (var j = 0; j < Player.MaxPlayers; j++)
        {
            if (j >= game.Players.Count || j == p.Index)
            {
                p.Relations[j] = 0;
                continue;
            }

            var v = _attitudeByDraw[game.Random.Next(6)];

            // Harder games make computer players like each other more and
            // everyone else less, by half the difficulty (truncated).
            var bothAi = p.IsAi && game.Players[j].IsAi;
            v += bothAi ? d / 2 : -(d / 2);

            var s = p.Species;
            if (s.Is(Species.Warrior))
            {
                v -= 2;
            }

            if (s.Is(Species.Nomad))
            {
                v -= 1;
            }

            if (s.Has(Species.Predator))
            {
                v -= 3;
            }

            if (s.Is(Species.Scientist))
            {
                v += 3;
            }

            if (s.Is(Species.Politician))
            {
                v += 2;
            }

            if (s.Is(Species.Merchant))
            {
                v += 1;
            }

            if (v == 0)
            {
                v = bothAi ? 1 : -1;
            }

            p.Relations[j] = Math.Clamp(v, -3, 3);
        }
    }

    private static void SetScores(Game game, Player p)
    {
        for (var j = 0; j < Player.MaxPlayers; j++)
        {
            if (j != p.Index && j < game.Players.Count)
            {
                p.RelationScores[j] = p.Relations[j] + 10 + game.Random.Next(40);
            }
        }
    }
}
