using OpenPax.Render;
using OpenPax.Ui.Forms;
using OpenPax.Ui.Screens;
using OpenPax.Ui.Widgets;
using static OpenPax.Ui.Tests.UiGames;

namespace OpenPax.Ui.Tests;

/// <summary>Game Start's Options: the player's name, flag and fleet reach Game Setup; the main buttons make room for it.</summary>
public sealed class OptionsTests
{
    private static OptionsModel Open(UiApp app)
    {
        var options = ((GameStartScreen)app.Current).OptionsPane;
        Click(app, options.Left + 5, options.Top + 5);
        Assert.Equal(ScreenMode.Options, app.Mode);
        return (OptionsModel)((FormScreen)app.Current).Form.CodeBehind!; // The screen's model is the form's code-behind.
    }

    [Fact]
    public void SixMainButtonsShareTheFivesSpanWithOptionsThird()
    {
        var app = NewApp();
        var screen = (GameStartScreen)app.Current;
        var (single, exit) = (screen.Window.Get(31100), screen.Window.Get(31104));
        Assert.True(screen.Window.Get(31101).Top < screen.OptionsPane.Top);
        Assert.True(screen.OptionsPane.Top < screen.Window.Get(31102).Top);
        Assert.True(single.Top < exit.Top);
        Assert.Equal(UiText.MenuOptions, screen.OptionsPane.Caption);
    }

    [Fact]
    public void APressedMainButtonShowsTheArtWhereItIs()
    {
        var app = NewApp();
        var screen = (GameStartScreen)app.Current;
        var button = screen.Window.Get(31103); // moved down to make room for Options, off the place its pictures were cut for
        byte[] Strip()
        {
            var frame = new Framebuffer(640, 480);
            app.Draw(frame);
            return [.. Enumerable.Range(button.Top, button.Height).SelectMany(y => frame.Pixels.AsSpan((y * 640) + button.Left, 6).ToArray())];
        }

        var plain = Strip();
        app.MouseDown(button.Left + (button.Width / 2), button.Top + (button.Height / 2), 1, 0);
        Assert.True(((ButtonPane)button).Pressed);
        Assert.Equal(plain, Strip());
    }

    [Fact]
    public void ThePlayersChoicesStartGameSetup()
    {
        var app = NewApp();
        var model = Open(app);
        model.Name = "Jeff";
        model.Flag = 5;
        model.Fleet = 3;
        Assert.Equal(4, app.Preferences.Get(Preferences.FlagId));
        Assert.Equal(2, app.Preferences.Get(Preferences.FleetId));
        Assert.Equal("Empire:3004", model.FlagImage);
        model.OnDone();
        Assert.Equal(ScreenMode.GameStart, app.Mode);

        Click(app, 400, 110);
        Click(app, 400, 110);
        Click(app, 550, 375);
        Assert.Equal(ScreenMode.GameSetup, app.Mode);
        Assert.Equal("Jeff", app.Current.Window.Get<TextEditPane>(10005).Text);
        Assert.Equal(4, app.Current.Window.Get<FlagPane>(10007).Flag);
        Assert.Equal(2, app.Current.Window.Get<FleetColorPane>(10006).Color);
    }

    [Fact]
    public void DisplaySettingsAreKeptAtOnce()
    {
        var app = NewApp();
        var model = Open(app);
        model.Fullscreen = true;
        model.TextFont = 0;
        Assert.True(app.Preferences.Flag(Preferences.Fullscreen));
        Assert.Equal(0, app.Preferences.Get(Preferences.TextFont));
    }

    [Fact]
    public void TheStarSpeedSliderRunsFromStillToTwiceTheDefault()
    {
        var app = NewApp();
        var model = Open(app);
        Assert.Equal(50, model.StarSpeed);
        model.StarSpeed = 100;
        _ = app.Tick(0).ToList();
        Assert.Equal(OpenPax.Forms.Drawing.WarpField.DefaultSpeed * 2, app.Forms.Theme.Warp!.Speed, 6); // Not null: moving stars are on by default.
        model.StarSpeed = 0;
        _ = app.Tick(16).ToList();
        Assert.Equal(0f, app.Forms.Theme.Warp!.Speed); // As above.
        Assert.Equal("Star speed: 0%", model.StarSpeedText);
    }

    [Fact]
    public void SlowingToSpeedZeroPausesASinglePlayerGame()
    {
        var app = InGame();
        var clock = app.Session!.Clock;
        Assert.True(clock.ZeroPauses); // on by default
        while (clock.Speed > 0)
        {
            app.Command(UiCommand.SlowDown);
        }

        Assert.True(clock.Held);
        Assert.Contains(app.Log.History.Concat(app.Log.Waiting), n => n?.Text.Contains("Paused", StringComparison.OrdinalIgnoreCase) == true);
        var tick = app.Session.Game.Tick;
        for (var t = 0L; t < 5000; t += 100)
        {
            _ = app.Tick(t).ToList();
        }

        Assert.Equal(tick, app.Session.Game.Tick);
    }

    [Fact]
    public void TheGameplayTabHoldsEveryFixAndANewGameTakesThem()
    {
        var app = NewApp();
        var model = Open(app);
        var form = ((FormScreen)app.Current).Form;
        var tabs = form.Descendants().OfType<OpenPax.Forms.Controls.TabControl>().Single();
        tabs.SelectedIndex = 1;
        _ = app.Tick(0).ToList();
        app.DrawCanvas(new OpenPax.Render.TrueColor.Canvas(1280, 960), OpenPax.Render.ClassicPlacement.For(1280, 960, 640, 480));
        var boxes = Preferences.FixNames.Values.Select(n => form.Get<OpenPax.Forms.Controls.CheckBox>(n)).ToList();
        Assert.Equal(12, boxes.Count);
        Assert.All(boxes, b => Assert.True(b.Bounds.Bottom <= tabs.Bounds.Bottom && b.Bounds.Right <= tabs.Bounds.Right, b.Name + " fits"));

        model.FixFleetsJoinBattles = true;
        model.FixExperienceOnce = true;
        model.OnDone();
        var game = InGame(app);
        Assert.Equal(OpenPax.Sim.Fixes.FleetsJoinBattles | OpenPax.Sim.Fixes.ExperienceOnce, game.Session!.Game.Fixes);
    }
}
