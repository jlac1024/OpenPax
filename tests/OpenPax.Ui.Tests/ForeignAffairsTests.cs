using OpenPax.Sim.Players;
using OpenPax.Ui.Screens;
using OpenPax.Ui.Widgets;
using static OpenPax.Ui.Tests.UiGames;

namespace OpenPax.Ui.Tests;

public class ForeignAffairsTests
{
    /// <summary>In a game, at peace with the first other player, in the Foreign Affairs window.</summary>
    private static (UiApp App, GameSession Session, Player Other) AtForeign()
    {
        var app = InGame();
        var session = app.Session!;
        var other = session.Game.Players.First(p => p != session.Local);
        session.Game.ReplyTreaty(session.Local, other, Treaty.Neutral, Treaty.InForce);
        app.Command(UiCommand.ForeignAffairs);
        Assert.Equal(ScreenMode.ForeignAffairs, app.Mode);
        return (app, session, other);
    }

    private static void Press(UiApp app, int id)
    {
        var window = app.Popup?.Window ?? app.Current.Window;
        var pane = window.Get(id);
        Assert.True(pane.Shown && pane.Enabled, $"{id} cannot be pressed");
        Click(app, pane.Left + 5, pane.Top + 5);
    }

    [Fact]
    public void TheFirstTabIsPickedWithItsStanding()
    {
        var (app, session, other) = AtForeign();
        var window = app.Current.Window;
        Assert.Equal(session.Game.Players.Count - 1, window.Get<ListBoxPane>(15032).Rows.Count);
        Assert.Same(other, ((ForeignAffairsScreen)app.Current).Picked);
        Assert.Equal(other.Index, session.ForeignPlayer);
        Assert.Equal("Neutral", window.Get<TextLinePane>(15024).Text);
        Assert.Equal(1, window.Get<StateBoxPane>(15013).Value);
        Assert.True(window.Get(15014).Enabled);
        Assert.False(window.Get(15016).Enabled);

        app.Tick(100);
        app.Draw(new OpenPax.Render.Framebuffer(640, 480));
    }

    [Fact]
    public void ATreatyBoxOffersTheTreaty()
    {
        var (app, session, other) = AtForeign();
        Press(app, 15014);
        Assert.Equal(Treaty.OfferedByMe, session.Local.Treaties[other.Index].Slot(Treaty.TradeRoutes));
        Assert.Equal(2, app.Current.Window.Get<StateBoxPane>(15014).Value);
    }

    [Fact]
    public void AnOfferIsViewedAndAccepted()
    {
        var (app, session, other) = AtForeign();
        session.Game.OfferTreaty(other, session.Local, Treaty.Resupply);
        app.Tick(0).ToList();
        var offers = app.Current.Window.Get<ListBoxPane>(15033);
        var row = Assert.IsType<TreatyOfferRow>(Assert.Single(offers.Rows));
        Assert.Equal(Treaty.Resupply, row.State);

        Press(app, 15009);
        Assert.IsType<OfferPopup>(app.Popup);
        Press(app, 43002);
        Assert.Null(app.Popup);
        Assert.True(session.Local.Treaties[other.Index].Has(Treaty.Resupply));
        Assert.Empty(offers.Rows);
    }

    [Fact]
    public void AMissionIsStartedAndAborted()
    {
        var (app, session, other) = AtForeign();
        Press(app, 15004);
        var popup = Assert.IsType<MissionPopup>(app.Popup);
        Assert.Equal(SpyMission.StealShips, popup.Kind);
        Press(app, 40004);
        Assert.Equal(SpyMission.StealTechs, popup.Kind);
        Press(app, 40001);

        var mission = Assert.Single(session.Local.Missions);
        Assert.Same(other, mission.Target);
        app.Tick(0).ToList();
        var missions = app.Current.Window.Get<ListBoxPane>(15034);
        Assert.Single(missions.Rows);

        Press(app, 15010);
        Assert.StartsWith("Are you sure you want to abort", app.Popup!.Window.Get<WrappedTextPane>(38503).Text);
        Press(app, 38501);
        Assert.Empty(session.Local.Missions);
        app.Tick(0).ToList();
        Assert.Empty(missions.Rows);
    }

    [Fact]
    public void ATradePackageIsMadeAndSent()
    {
        var (app, session, other) = AtForeign();
        Press(app, 15002);
        var popup = Assert.IsType<TradePopup>(app.Popup);

        Press(app, 41203);
        var picker = popup.Window.Get<ListBoxPane>(41309);
        var ship = (TradeItemRow)picker.Selected!;
        Press(app, 41302);
        Assert.Equal([ship.Value], popup.Package.Offered.Ships);
        Assert.DoesNotContain(picker.Rows.Cast<TradeItemRow>(), r => r.Value == ship.Value);

        Press(app, 41307);
        var spinner = popup.Window.Get<SpinnerPane>(41308);
        Click(app, spinner.Left + spinner.Width - 5, spinner.Top + 6);
        Assert.Equal(1000, popup.Package.Offered.Credits);

        Press(app, 41301);
        Assert.Equal(2, popup.Window.Get<ListBoxPane>(41207).Rows.Count);
        Press(app, 41201);
        Assert.Null(app.Popup);
        var sent = Assert.Single(other.Packages);
        Assert.Equal([ship.Value], sent.Offered.Ships);
    }

    [Fact]
    public void TheBridgeStationOpensIt()
    {
        var app = InGame();
        var station = app.Current.Window.Get(22003);
        Click(app, station.Left + 5, station.Top + 5);
        Assert.Equal(ScreenMode.ForeignAffairs, app.Mode);
    }
}
