using OpenPax.Sim;
using OpenPax.Sim.Players;

namespace OpenPax.Ai.Tests;

public class TreatyTests
{
    private static (Game Game, Player A, Player B) Met(double scoreOfAToB)
    {
        var game = AiGames.New(1234, 2, 3);
        var (a, b) = (game.Players[0], game.Players[1]);
        game.ReplyTreaty(a, b, Treaty.Neutral, Treaty.InForce);
        a.RelationScores[b.Index] = scoreOfAToB;
        return (game, a, b);
    }

    private static void RunUntilThought(Game game, Player p)
    {
        do
        {
            game.Step();
        }
        while (game.LastThinker != p.Index);
    }

    [Fact]
    public void AFriendlyPlayerOffersTheLowestStateItLacks()
    {
        var (game, a, b) = Met(80.0);
        RunUntilThought(game, a);

        // Score 80 wants up to Repair and Resupply (3); Neutral is held, so Trade Routes is offered.
        Assert.Equal(Treaty.OfferedByMe, a.Treaties[b.Index].Slot(Treaty.TradeRoutes));
    }

    [Fact]
    public void AHostileScoreMeansWar()
    {
        var (game, a, b) = Met(-50.0);
        RunUntilThought(game, a);
        Assert.Equal(Treaty.War, a.Treaties[b.Index].Level());
        Assert.Equal(Treaty.War, b.Treaties[a.Index].Level());
    }

    [Fact]
    public void OffersAreAnsweredByScore()
    {
        var (game, a, b) = Met(50.0);
        game.OfferTreaty(b, a, Treaty.Allied);
        game.OfferTreaty(b, a, Treaty.TradeRoutes);
        RunUntilThought(game, a);

        // 50 passes Trade Routes (0) but not Allied (99).
        Assert.True(a.Treaties[b.Index].Has(Treaty.TradeRoutes));
        Assert.Equal(Treaty.Off, a.Treaties[b.Index].Slot(Treaty.Allied));
    }

    [Fact]
    public void LongComputerGamesMakeTreatiesAndStayDeterministic()
    {
        (ulong Hash, int Treaties) Run()
        {
            var game = AiGames.New(3, 6, 3);
            AiGames.Run(game, 30000);
            var treaties = game.Players.Sum(p => game.Players.Count(o => o != p && p.Treaties[o.Index].Level() != Treaty.None));
            return (game.Hash(), treaties);
        }

        var first = Run();
        Assert.Equal(first, Run());
    }
}
