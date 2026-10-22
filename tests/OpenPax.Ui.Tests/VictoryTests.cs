using OpenPax.Sim;
using OpenPax.Sim.Galaxy;
using OpenPax.Ui.Screens;
using OpenPax.Ui.Widgets;
using static OpenPax.Ui.Tests.UiGames;

namespace OpenPax.Ui.Tests;

public class VictoryTests
{
    private static void Press(UiApp app, int id)
    {
        var pane = app.Current.Window.Get(id);
        Assert.True(pane.Shown && pane.Enabled, $"{id} cannot be pressed");
        Click(app, pane.Left + 5, pane.Top + 5);
    }

    [Fact]
    public void VictoryOpensTheEndWindowAndPlaysItsMovie()
    {
        var app = InGame();
        var sound = new RecordingSound();
        app.Sound = sound;
        app.GameEvents(StepEvents.Victory);
        Assert.Equal(ScreenMode.Victory, app.Mode);
        Assert.Contains(36800, sound.Played);
        Assert.StartsWith("VICTORY", app.Current.Window.Get<TextLinePane>(16001).Text);
        Assert.Equal(16005, app.Current.Window.Get<ButtonPane>(16002).Picture);
        _ = app.Tick(1).ToList();
        app.Draw(new OpenPax.Render.Framebuffer(640, 480));
    }

    [Fact]
    public void ADefeatedPlayerSeesDefeatAndCanObserveEverySystem()
    {
        var app = InGame();
        var session = app.Session!;
        session.Local.Eliminated = true;
        app.GameEvents(StepEvents.Defeat);
        Assert.Equal(ScreenMode.Victory, app.Mode);
        Assert.StartsWith("DEFEAT", app.Current.Window.Get<TextLinePane>(16001).Text);
        Assert.Equal(14, app.PaletteIndex);
        Press(app, 16004);
        Assert.Equal(ScreenMode.MainControl, app.Mode);
        Assert.All(session.Game.Universe.Stars, s => Assert.NotEqual(Star.Unseen, s.Sight[session.Local.Index]));
    }

    [Fact]
    public void NewGameDropsTheGame()
    {
        var app = InGame();
        app.GameEvents(StepEvents.Victory);
        Press(app, 16003);
        Assert.Equal(ScreenMode.GameStart, app.Mode);
        Assert.Null(app.Session);
    }
}
