using OpenPax.Sim;
using OpenPax.Ui.Screens;
using OpenPax.Ui.Widgets;
using static OpenPax.Ui.Tests.UiGames;

namespace OpenPax.Ui.Tests;

public class MessageLogTests
{
    private static UiNotice Lasting(string text, int priority = 0) => new(text, priority, false, null);

    private static UiNotice Transient(string text) => new(text, 0, true, null);

    [Fact]
    public void LastingNoticesWaitTheirTurnEveryThreeSeconds()
    {
        var log = new MessageLog();
        log.Tick(0);
        log.Add(Lasting("a"), 0);
        log.Add(Lasting("b"), 0);
        Assert.Null(log.Current);

        log.Tick(3000);
        Assert.Equal("a", log.Current?.Text);
        log.Tick(5999);
        Assert.Equal("a", log.Current?.Text);
        log.Tick(6000);
        Assert.Equal("b", log.Current?.Text);
        Assert.Equal("a", log.History[1]?.Text);
    }

    [Fact]
    public void ATransientNoticeShowsAtOnceAndIsNeverKept()
    {
        var log = new MessageLog();
        log.Tick(0);
        log.Add(Transient("speed"), 0);
        Assert.Equal("speed", log.Current?.Text);

        // A lasting notice takes its place at once and the transient one is gone.
        log.Add(Lasting("research"), 10);
        Assert.Equal("research", log.Current?.Text);
        Assert.Null(log.History[1]);
    }

    [Fact]
    public void ANewLastingNoticeBlinksNineTimes()
    {
        var log = new MessageLog();
        log.Tick(0);
        log.Add(Transient("x"), 0);
        log.Add(Lasting("y"), 0);
        Assert.True(log.Blinking);
        for (var t = 250; t <= 9 * 250; t += 250)
        {
            log.Tick(t);
        }

        Assert.False(log.Blinking);
    }

    [Fact]
    public void TheHistoryKeepsOneHundredFiftyNewestFirst()
    {
        var log = new MessageLog();
        log.Tick(0);
        for (var i = 0; i < 160; i++)
        {
            log.Add(Lasting(i.ToString(System.Globalization.CultureInfo.InvariantCulture)), 0);
        }

        for (var t = 250L; t <= 160 * 3000; t += 250)
        {
            log.Tick(t);
        }

        Assert.Equal("159", log.History[0]?.Text);
        Assert.Equal("10", log.History[149]?.Text);
    }

    [Fact]
    public void AGameNoticeReachesTheLogWithTheDate()
    {
        var app = InGame();
        var session = app.Session!;
        var proposer = session.Game.Players.First(p => p != session.Local);
        var gift = new Sim.Players.TradePackage(proposer, session.Local);
        gift.Offered.Credits = 10;
        session.Game.OfferPackage(gift);

        for (var t = 0L; t <= 30000 && !app.Log.History.Any(n => n?.Text.Contains("Trade package Offered by ", StringComparison.Ordinal) ?? false); t += 250)
        {
            _ = app.Tick(t).ToList();
        }

        var notice = app.Log.History.First(n => n?.Text.Contains("Trade package Offered by ", StringComparison.Ordinal) ?? false)!;
        Assert.Equal(session.Date + ": Trade package Offered by " + proposer.Name, notice.Text);
        Assert.Equal(1, notice.Priority);
    }

    [Fact]
    public void TheBackButtonReturnsToTheScreenLeft()
    {
        var app = InGame();
        app.Command(UiCommand.SectorDisplay);
        Assert.Empty(app.BackStack);

        // Going back from the Sector Display with nothing remembered is the bridge.
        app.Back();
        Assert.Equal(ScreenMode.MainControl, app.Mode);
    }

    [Fact]
    public void TheTopBarSitsOnGameWindowsOnly()
    {
        var app = InGame();
        Assert.DoesNotContain(app.Current.Window.Root.Children, p => p is TopBarPane);
        app.Command(UiCommand.SectorDisplay);
        Assert.IsType<TopBarPane>(app.Current.Window.Root.Children[^1]);

        // Pressing the back button there goes to the bridge.
        Click(app, 610, 8);
        Assert.Equal(ScreenMode.MainControl, app.Mode);
    }

    [Fact]
    public void TheLampOpensTheHistory()
    {
        var app = InGame();
        app.Command(UiCommand.SectorDisplay);
        var bar = (TopBarPane)app.Current.Window.Root.Children[^1];
        Click(app, 505, 8);
        Assert.True(bar.HistoryOpen);
        Click(app, 505, 8);
        Assert.False(bar.HistoryOpen);
    }

    [Fact]
    public void ADoubleClickOnAShipNoticeShowsItsYard()
    {
        var app = InGame();
        var session = app.Session!;
        var home = session.Local.Colonies[0].Planet;
        var notice = NoticeText.Make(app, session, new GameNotice(NoticeKind.ShipFinished, session.Local, Place: home,
            Ship: session.Local.Designs.First(d => d is not null)));
        notice.Action!(app);

        Assert.Equal(ScreenMode.SectorDisplay, app.Mode);
        Assert.Equal(home, session.Selection);
    }
}
