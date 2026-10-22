using OpenPax.Render;
using OpenPax.Ui.Screens;
using OpenPax.Ui.Widgets;
using static OpenPax.Ui.Tests.UiGames;

namespace OpenPax.Ui.Tests;

public class BridgeAndMapTests
{
    [Fact]
    public void TheGameOpensOnTheBridgeWithItsDate()
    {
        var app = InGame();
        Assert.Equal(ScreenMode.MainControl, app.Mode);
        _ = app.Tick(1).ToList();
        Assert.Equal("2100.0", app.Current.Window.Get<TextLinePane>(22109).Text);
    }

    [Fact]
    public void TheClockRunsATickPerIntervalAndNoMore()
    {
        var app = InGame();
        var game = app.Session!.Game;

        // Speed 3 is 150 ms a tick; one tick at most per frame, however late.
        _ = app.Tick(149).ToList();
        Assert.Equal(0, game.Tick);
        _ = app.Tick(150).ToList();
        Assert.Equal(1, game.Tick);
        _ = app.Tick(1000).ToList();
        Assert.Equal(2, game.Tick);
    }

    [Fact]
    public void PauseStopsTheClockAndSaysSo()
    {
        var app = InGame();
        app.Command(UiCommand.Pause);

        // Notices from setup (first contact with a neighbor) are queued ahead of it.
        for (var t = 0L; t <= 20000 && !app.Notice.EndsWith("Game Paused", StringComparison.Ordinal); t += 250)
        {
            _ = app.Tick(t).ToList();
        }

        Assert.Equal(0, app.Session!.Game.Tick);
        Assert.EndsWith("Game Paused", app.Notice);

        app.Command(UiCommand.Pause);
        var resumed = app.Session.Game.Tick;
        _ = app.Tick(30000).ToList();
        Assert.Equal(resumed + 1, app.Session.Game.Tick);
    }

    [Fact]
    public void SpeedKeysStayWithinTheSevenSpeeds()
    {
        var app = InGame();
        for (var i = 0; i < 10; i++)
        {
            app.Command(UiCommand.SpeedUp);
        }

        Assert.Equal(6, app.Session!.Clock.Speed);
        Assert.Equal(13, app.Session.Clock.Interval);
        app.Command(UiCommand.SlowDown);
        Assert.EndsWith("Speed set to 5", app.Notice);
    }

    [Fact]
    public void FunctionKeysSwitchBetweenBridgeAndSectorDisplay()
    {
        var app = InGame();
        app.Command(UiCommand.SectorDisplay);
        Assert.Equal(ScreenMode.SectorDisplay, app.Mode);
        app.Command(UiCommand.Bridge);
        Assert.Equal(ScreenMode.MainControl, app.Mode);
    }

    [Fact]
    public void ScreenKeysDoNothingOutsideTheGame()
    {
        var app = AtGameSetup();
        app.Command(UiCommand.SectorDisplay);
        Assert.Equal(ScreenMode.GameSetup, app.Mode);
    }

    [Fact]
    public void ClickingAStarOnTheMapMakesItCurrent()
    {
        var app = InGame();
        app.Command(UiCommand.SectorDisplay);
        var map = app.Current.Window.Get<MapPane>(13101);
        var session = app.Session!;
        var other = session.Game.Universe.Stars.First(s => s.Index != session.CurrentStar);
        var (x, y) = map.ToPane(other.X, other.Y);

        Click(app, x, y);
        Assert.Equal(other.Index, session.CurrentStar);
    }

    [Fact]
    public void ADoubleClickOnTheBridgeMapOpensTheSectorDisplay()
    {
        var app = InGame();
        var map = app.Current.Window.Get<MapPane>(22008);
        var home = app.Session!.Game.Universe.Stars[app.Session.CurrentStar];
        var (x, y) = map.ToPane(home.X, home.Y);

        Click(app, x, y);
        Click(app, x, y, clicks: 2);
        Assert.Equal(ScreenMode.SectorDisplay, app.Mode);
    }

    [Fact]
    public void TheHomeStarIsInViewOnTheMap()
    {
        var app = InGame();
        var session = app.Session!;
        var home = session.Game.Universe.Stars[session.CurrentStar];
        Assert.Equal(OpenPax.Sim.Galaxy.Star.InView, home.Sight[session.Local.Index]);

        app.Command(UiCommand.SectorDisplay);
        var frame = new Framebuffer(640, 480);
        app.Draw(frame);
        Assert.Contains(frame.Pixels, p => p != 0);
    }
}
