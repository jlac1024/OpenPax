using OpenPax.Sim.Players;
using OpenPax.Ui.Screens;
using OpenPax.Ui.Widgets;
using static OpenPax.Ui.Tests.UiGames;

namespace OpenPax.Ui.Tests;

public class GameOptionsTests
{
    private static (UiApp App, GameSession Session, GameOptionsScreen Screen) AtOptions()
    {
        var app = InGame();
        app.Command(UiCommand.GameOptions);
        Assert.Equal(ScreenMode.GameOptions, app.Mode);
        return (app, app.Session!, (GameOptionsScreen)app.Current);
    }

    private static void Press(UiApp app, int id)
    {
        var window = app.Popup?.Window ?? app.Current.Window;
        var pane = window.Get(id);
        Assert.True(pane.Shown && pane.Enabled, $"{id} cannot be pressed");
        Click(app, pane.Left + 5, pane.Top + 5);
    }

    [Fact]
    public void TheClockWaitsWhileTheWindowIsOpen()
    {
        var (app, session, screen) = AtOptions();
        Assert.True(session.Clock.Paused);
        Assert.Equal(32003, screen.Panel);
        app.Draw(new OpenPax.Render.Framebuffer(640, 480));

        app.Command(UiCommand.Bridge);
        Assert.False(session.Clock.Paused);
    }

    [Fact]
    public void TheOptionsSetThePreferences()
    {
        var (app, session, _) = AtOptions();
        Press(app, 32053);
        Assert.False(app.Preferences.Flag(Preferences.PlayMovies));

        var speed = app.Current.Window.Get<SpinnerPane>(32056);
        Assert.Equal(3, speed.Value);
        Click(app, speed.Left + speed.Width - 5, speed.Top + 6);
        Assert.Equal(4, app.Preferences.Get(Preferences.GameSpeed));
        Assert.Equal(4, session.Clock.Speed);
    }

    [Fact]
    public void AVolumeSliderStoresTwelvePerStep()
    {
        var (app, _, _) = AtOptions();
        Press(app, 32006);
        var slider = app.Current.Window.Get<VolumeSliderPane>(32031);
        Click(app, slider.Left + 10, slider.Top + 13);
        Assert.Equal(10, slider.Value);
        Assert.Equal(120, app.Preferences.Get(Preferences.SoundFxVolume));
        app.Draw(new OpenPax.Render.Framebuffer(640, 480));
    }

    [Fact]
    public void AProfileIsCopiedRenamedAndDeleted()
    {
        var (app, session, screen) = AtOptions();
        var player = session.Local;
        Press(app, 32051);
        Assert.Equal(0, screen.PlanSlot);
        Assert.False(app.Current.Window.Get(32026).Enabled);

        Press(app, 32025);
        Assert.Equal(6, screen.PlanSlot);
        Assert.Equal(player.Plans[0], player.Plans[6]);
        Assert.Equal("Unnamed", player.PlanNames[6]);
        Assert.Equal(50, app.Current.Window.Get<ListBoxPane>(32021).Rows.Count);

        var edit = app.Current.Window.Get<TextEditPane>(32013);
        Click(app, edit.Left + 5, edit.Top + 5);
        app.Typed("X", 0);
        app.Tick(0).ToList();
        Assert.EndsWith("X", player.PlanNames[6]);

        Press(app, 32026);
        Press(app, 38501);
        Assert.Null(player.Plans[6]);
        Assert.Equal(0, screen.PlanSlot);
    }

    [Fact]
    public void TheBridgeStationOpensIt()
    {
        var app = InGame();
        var station = app.Current.Window.Get(22007);
        Click(app, station.Left + 5, station.Top + 5);
        Assert.Equal(ScreenMode.GameOptions, app.Mode);
    }

    [Fact]
    public void NewGameDropsTheGame()
    {
        var (app, _, _) = AtOptions();
        Press(app, 32004);
        Assert.Null(app.Session);
        Assert.Equal(ScreenMode.GameStart, app.Mode);
    }
}
