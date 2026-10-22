using OpenPax.Sim.Players;
using OpenPax.Sim.Setup;

namespace OpenPax.Sim.Tests;

public class TreatyClickTests
{
    private static (Game Game, Player Me, Player Other) NewGame2()
    {
        var tables = GameTablesForTests.Load();
        var game = NewGame.Create(new GameSetup(1234, 30, 2, 3, [new HumanPlayerSetup("Commander", 0, 0, tables.Species[0])]), tables);
        return (game, game.Players[0], game.Players[1]);
    }

    private static int Slot(Player a, Player b, int state) => a.Treaties[b.Index].Slot(state);

    [Fact]
    public void WarIsDeclaredAtOnceAndPeaceIsOffered()
    {
        var (game, me, other) = NewGame2();
        game.TreatyClick(me, other, Treaty.War);
        Assert.Equal(Treaty.InForce, Slot(me, other, Treaty.War));
        Assert.Equal(Treaty.InForce, Slot(other, me, Treaty.War));
        Assert.False(Game.TreatyOpen(me, other, Treaty.War));

        game.TreatyClick(me, other, Treaty.Neutral);
        Assert.Equal(Treaty.OfferedByMe, Slot(me, other, Treaty.Neutral));
        Assert.Equal(Treaty.OfferedToMe, Slot(other, me, Treaty.Neutral));

        // A second click withdraws the offer.
        game.TreatyClick(me, other, Treaty.Neutral);
        Assert.Equal(Treaty.Off, Slot(me, other, Treaty.Neutral));
    }

    [Fact]
    public void TradeRoutesNeedNeutralAndAreOfferedThenCanceled()
    {
        var (game, me, other) = NewGame2();
        Assert.False(Game.TreatyOpen(me, other, Treaty.TradeRoutes));
        game.ReplyTreaty(me, other, Treaty.Neutral, Treaty.InForce);
        Assert.True(Game.TreatyOpen(me, other, Treaty.TradeRoutes));
        Assert.False(Game.TreatyOpen(me, other, Treaty.Allied));

        game.TreatyClick(me, other, Treaty.TradeRoutes);
        Assert.Equal(Treaty.OfferedByMe, Slot(me, other, Treaty.TradeRoutes));
        game.ReplyTreaty(other, me, Treaty.TradeRoutes, Treaty.InForce);
        game.TreatyClick(me, other, Treaty.TradeRoutes);
        Assert.Equal(Treaty.Off, Slot(me, other, Treaty.TradeRoutes));
    }

    [Fact]
    public void AnOfferToMeIsNotAnsweredByTheBox()
    {
        var (game, me, other) = NewGame2();
        game.ReplyTreaty(me, other, Treaty.Neutral, Treaty.InForce);
        game.OfferTreaty(other, me, Treaty.Resupply);
        game.TreatyClick(me, other, Treaty.Resupply);
        Assert.Equal(Treaty.OfferedToMe, Slot(me, other, Treaty.Resupply));
    }

    [Fact]
    public void ThePreviewDoublesTheChanceForSpies()
    {
        var (cost, chance) = SpyMission.Preview(SpyMission.StealShips, 1, false);
        Assert.Equal(100, cost);
        Assert.Equal(22, chance);
        // 0.15 x 1.5 rounds to just under 0.225, so doubled it reads 44.
        Assert.Equal(44, SpyMission.Preview(SpyMission.StealShips, 1, true).ChancePercent);
    }
}
