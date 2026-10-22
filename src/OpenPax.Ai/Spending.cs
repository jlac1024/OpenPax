using OpenPax.Sim;
using OpenPax.Sim.Numerics;
using OpenPax.Sim.Players;

namespace OpenPax.Ai;

/// <summary>Construction funding and spending percents of a computer player.</summary>
internal static class Spending
{
    public static void ClearFunding(Player p)
    {
        foreach (var c in p.Colonies)
        {
            Game.SetFunding(c, 0.0);
        }
    }

    /// <summary>
    /// Above difficulty 1 and with 500 credits or more, (money - 1000) / 20
    /// is shared between colonies with building to do, at most 2000 each.
    /// </summary>
    public static void Fund(Game game, Player p)
    {
        if (!(game.Setup.Difficulty > 1 && p.Money >= 1000.0 * 0.5))
        {
            return;
        }

        var budget = X87.ToInt((p.Money - 1000.0) / 20.0);

        // at war with someone.
        var war = game.Players.Any(o => o != p && p.Treaties[o.Index].Level() < 1);

        bool Needy(Colony c) => Game.Damaged(c) is not null
            || c.FirstOfKind(TechInfo.ShipyardKind) is { Queue.Count: > 0 }
            || (!war && game.NextImprovementId(c) >= 0);

        var needy = p.Colonies.Count(Needy);
        if (needy < 1 || budget < 1)
        {
            ClearFunding(p);
            return;
        }

        var share = budget / needy;
        if (share > 1999)
        {
            share = 2000;
        }

        foreach (var c in p.Colonies)
        {
            Game.SetFunding(c, Needy(c) ? share : 0.0);
        }
    }

    /// <summary>Espionage only with someone met, construction always, research while anything is left.</summary>
    public static void SetPercents(Game game, Player p)
    {
        var met = game.Players.Any(o => o != p && p.Treaties[o.Index].Level() != Treaty.None);
        Game.SetSpending(p, 1, met ? 100.0 : 0.0);
        Game.SetSpending(p, 2, 100.0);
        Game.SetSpending(p, 0, game.AnythingToResearch(p) ? 100.0 : 0.0);
    }
}
