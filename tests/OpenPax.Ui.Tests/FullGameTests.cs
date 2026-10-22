using OpenPax.Render;
using OpenPax.Ui.Screens;
using static OpenPax.Ui.Tests.UiGames;

namespace OpenPax.Ui.Tests;

/// <summary>
/// A whole single player game through the app, as the Phase 4 exit asks:
/// new game, the clock running, a quick save and load partway, and the end
/// window when the other empires are gone.
/// </summary>
public class FullGameTests
{
    private const int StepTime = 1000;

    /// <summary>Runs the app's frames until the game has taken the steps, drawing now and then.</summary>
    private static long Play(UiApp app, long now, int steps)
    {
        var frame = new Framebuffer(640, 480);
        var target = app.Session!.Game.Tick + steps;
        for (var i = 0; app.Session!.Game.Tick < target && !app.Session.Game.Over; i++)
        {
            now += StepTime;
            _ = app.Tick(now).ToList();

            // A popup the computer players open (offers, trades) is closed so play goes on.
            if (app.Popup is not null)
            {
                app.Command(UiCommand.GameOptions);
            }

            if (i % 100 == 0)
            {
                app.Draw(frame);
            }

            Assert.True(i < steps * 50, "the game stopped stepping");
        }

        return now;
    }

    private static void Type(UiApp app, string line)
    {
        app.Typed(line, 0);
        app.Key(UiKey.Return, false, 0);
    }

    [Fact]
    public void ANewGamePlaysSavesLoadsAndEndsInVictory()
    {
        // The galaxy seed comes from the clock, so each run plays another galaxy.
        var app = InGame();
        var saves = new MemorySaves();
        app.SaveGames = saves;
        var now = Play(app, 0, 8000);

        // Saving is refused while a battle is fought.
        while (app.Session!.Game.InCombat)
        {
            now = Play(app, now, 1);
        }

        // Quick save, play on, quick load: the game is the saved one again,
        // and the same steps from it come out the same.
        app.Command(UiCommand.QuickSave);
        Assert.Contains(UiApp.QuickSaveName, saves.Files.Keys);
        var saved = app.Session.Game.Hash();
        now = Play(app, now, 1000);
        var after = app.Session.Game.Hash();
        app.Command(UiCommand.QuickLoad);
        _ = app.Tick(now).ToList();
        Assert.Equal(ScreenMode.MainControl, app.Mode);
        Assert.Equal(saved, app.Session.Game.Hash());
        now = Play(app, now, 1000);
        Assert.Equal(after, app.Session.Game.Hash());

        // The other empires go through the developer console, and the end window opens, won.
        app.Command(UiCommand.Console);
        Type(app, "cheats on");
        foreach (var player in app.Session.Game.Players.Where(p => p != app.Session.Local && !p.Eliminated).ToList())
        {
            Type(app, "kill " + player.Index);
        }

        app.Command(UiCommand.GameOptions);
        _ = app.Tick(now + StepTime).ToList();
        Assert.Equal(ScreenMode.Victory, app.Mode);
        Assert.Equal(MouseCursor.Victory, app.Assets.Cursor.Set);
        app.Draw(new Framebuffer(640, 480));
    }
}
