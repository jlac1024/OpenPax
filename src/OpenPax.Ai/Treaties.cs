using OpenPax.Sim;
using OpenPax.Sim.Numerics;
using OpenPax.Sim.Players;

namespace OpenPax.Ai;

/// <summary>
/// A computer player's treaties (AI_TAndT, +0x1018): it wants the highest
/// state its attitude score passes, answers offers by that, cancels what it
/// no longer wants and offers what it lacks.
/// </summary>
internal static class Treaties
{
    // Score a computer player needs, above, for states 1 to 6.
    private static readonly double[] _thresholds = [double.NaN, -1.0, 0.0, 74.0, 99.0, 144.0, 174.0];

    public static void Run(Game game, Player p, int period)
    {
        if (p.Eliminated)
        {
            return;
        }

        Answer(game, p);
        Trading.Answer(game, p);
        Cancel(game, p);
        Offer(game, p, period);
    }

    /// <summary>Whether the score toward a player passes a state's threshold.</summary>
    private static bool Accepts(Player p, int other, int state)
    {
        return state is >= 1 and <= 6 && p.RelationScores[other] > _thresholds[state];
    }

    /// <summary>The highest state the score toward a player passes; 0 means war.</summary>
    // (its return of the player index above 250 cannot happen: scores stay within 250)
    private static int Wanted(Player p, int other)
    {
        var score = p.RelationScores[other];
        for (var state = 1; state <= 6; state++)
        {
            if (score <= _thresholds[state])
            {
                return state - 1;
            }
        }

        return 6;
    }

    /// <summary>Every offer waiting is accepted when the score passes, else turned down.</summary>
    private static void Answer(Game game, Player p)
    {
        foreach (var other in game.Players.ToList())
        {
            if (other == p)
            {
                continue;
            }

            for (var state = 0; state < Treaty.States; state++)
            {
                if (p.Treaties[other.Index].Slot(state) == Treaty.OfferedToMe)
                {
                    game.ReplyTreaty(p, other, state, Accepts(p, other.Index, state) ? Treaty.InForce : Treaty.Off);
                }
            }
        }
    }

    /// <summary>
    /// Above the wanted state: withdraws its own offers and cancels
    /// treaties; an alliance drops to Neutral, and Neutral to war.
    /// </summary>
    private static void Cancel(Game game, Player p)
    {
        foreach (var other in game.Players.ToList())
        {
            if (other == p || p.Treaties[other.Index].Level() == Treaty.None)
            {
                continue;
            }

            var wanted = Wanted(p, other.Index);
            for (var state = 6; state > wanted; state--)
            {
                switch (p.Treaties[other.Index].Slot(state))
                {
                    case Treaty.OfferedByMe:
                        game.ReplyTreaty(p, other, state, Treaty.Off);
                        break;
                    case Treaty.InForce when state == Treaty.Allied:
                        game.ReplyTreaty(p, other, Treaty.Neutral, Treaty.InForce);
                        break;
                    case Treaty.InForce when state == Treaty.Neutral:
                        game.ReplyTreaty(p, other, Treaty.War, Treaty.InForce);
                        break;
                    case Treaty.InForce:
                        game.ReplyTreaty(p, other, state, Treaty.Off);
                        break;
                }
            }
        }
    }

    /// <summary>
    /// At most one offer per player met: the lowest wanted state not held.
    /// Humans are offered something only rarely (10, 5 or 2 runs in 1000 by period).
    /// </summary>
    private static void Offer(Game game, Player p, int period)
    {
        foreach (var other in game.Players.ToList())
        {
            if (other == p || other.Eliminated || p.Treaties[other.Index].Level() == Treaty.None)
            {
                continue;
            }

            var chance = 1000;
            if (!other.IsAi)
            {
                chance = X87.ToInt(100.0 / ((double)(400 / period) * 5.0) * 10.0);
            }

            if (game.Random.Next(1000) + 1 > chance)
            {
                continue;
            }

            var treaty = p.Treaties[other.Index];
            var wanted = Wanted(p, other.Index);
            for (var state = 1; state <= wanted; state++)
            {
                var slot = treaty.Slot(state);
                if (slot == Treaty.OfferedByMe)
                {
                    break;
                }

                // States 1 to 3 are not offered while an alliance is held or pending.
                if (slot == Treaty.Off && (state >= Treaty.Allied || treaty.Slot(Treaty.Allied) == Treaty.Off))
                {
                    game.OfferTreaty(p, other, state);
                    break;
                }
            }
        }
    }
}
