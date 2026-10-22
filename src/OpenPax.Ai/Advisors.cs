using OpenPax.Sim;
using OpenPax.Sim.Players;
using OpenPax.Sim.Setup;

namespace OpenPax.Ai;

/// <summary>
/// A computer player's advisors (+0x10A0): it replaces governors turned by
/// enemy spies and promotes its best governor into a field when it beats
/// the advisor there. It never hires from outside.
/// </summary>
internal static class Advisors
{
    public static void Run(Game game, Player p)
    {
        ReplaceTurnedGovernors(game, p);
        for (var field = 0; field < 4; field++)
        {
            Assign(game, p, field);
        }
    }

    /// <summary>Each governor working against its owner (skills summing below 0) is replaced by a new one.</summary>
    private static void ReplaceTurnedGovernors(Game game, Player p)
    {
        var restart = true;
        while (restart)
        {
            restart = false;
            foreach (var a in p.Advisors)
            {
                if (!a.IsGovernor)
                {
                    continue;
                }

                var sum = 0.0;
                for (var k = 0; k < 4; k++)
                {
                    sum = a.Value(k) + sum;
                }

                if (sum < 0.0)
                {
                    p.Advisors.Remove(a);
                    p.Advisors.Insert(0, PlayerCreation.NewAdvisor(game, p, Advisor.GovernorRole, a.Star));
                    restart = true;
                    break;
                }
            }
        }
    }

    /// <summary>The best governor for a field (the later one on ties) takes it when it does strictly better than the holder.</summary>
    private static void Assign(Game game, Player p, int field)
    {
        Advisor? best = null;
        var bestValue = -9999.9;
        foreach (var a in p.Advisors)
        {
            if (a.IsGovernor && bestValue <= a.Value(field))
            {
                bestValue = a.Value(field);
                best = a;
            }
        }

        if (best is null)
        {
            return;
        }

        var holder = p.Advisors.FirstOrDefault(a => a.Role == field && a.Star == Advisor.NoStar);
        if (holder is not null && best.Value(field) <= holder.Value(field))
        {
            return;
        }

        game.AppointGovernor(p, best.Id, field);
    }
}
