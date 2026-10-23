using OpenPax.Render;
using OpenPax.Ui.Screens;
using OpenPax.Ui.Widgets;
using static OpenPax.Ui.Tests.UiGames;

namespace OpenPax.Ui.Tests;

public class NewGameFlowTests
{
    [Fact]
    public void NewGameGoesToSpeciesSelectionAndAcceptToGameSetup()
    {
        var app = NewApp();
        Click(app, 400, 110);
        Click(app, 400, 110);
        Assert.Equal(ScreenMode.SpeciesSelection, app.Mode);

        Click(app, 550, 375);
        Assert.Equal(ScreenMode.GameSetup, app.Mode);
        Assert.Equal(app.StockSpecies[0], app.ChosenSpecies);
    }

    [Fact]
    public void ThePickedSpeciesShowsItsNameOnceItsFiguresHavePlayed()
    {
        var app = NewApp();
        Click(app, 400, 110);
        Click(app, 400, 110);

        // Next steps to slot 1; its small then its large figure play, 15 frames each at 30 fps.
        Click(app, 160, 255);
        for (var now = 0L; now <= 2000; now += 16)
        {
            _ = app.Tick(now).ToList();
        }

        var screen = (SpeciesSelectionScreen)app.Current;
        Assert.Equal(1, screen.Slot);
        Assert.Equal(app.StockSpecies[1].Name, screen.Window.Get<TextLinePane>(11100).Text);
        Assert.False(screen.Window.Get(11011).Enabled);
    }

    [Fact]
    public void AnOpenFigureGoesBackTheWayItCameBeforeTheNextComesOut()
    {
        var app = NewApp();
        var sound = new RecordingSound();
        app.Sound = sound;
        Click(app, 400, 110);
        Click(app, 400, 110);
        var screen = (SpeciesSelectionScreen)app.Current;
        var now = Run(app, 0, 2000);
        Assert.Equal(app.StockSpecies[0].Name, screen.Window.Get<TextLinePane>(11100).Text);

        // Next: the first species' large figure closes backward, then its small one goes back, then the second comes out.
        sound.Played.Clear();
        Click(app, 160, 255, now);
        var large = screen.Window.Get<AnimationPane>(11030);
        Assert.True(large is { Playing: true, Reverse: true, Visible: true });
        Assert.Equal("", screen.Window.Get<TextLinePane>(11100).Text);
        Run(app, now, now + 4000);

        Assert.Equal([20200, 20300, 20000, 20100], sound.Played.Where(id => id is >= 20000 and < 20400));
        Assert.Equal(0, screen.Window.Get<AnimationPane>(11020).Frame);
        Assert.False(large.Visible);
        Assert.True(screen.Window.Get(11031).Visible);
        Assert.Equal(app.StockSpecies[1].Name, screen.Window.Get<TextLinePane>(11100).Text);
    }

    private static long Run(UiApp app, long from, long to)
    {
        for (var now = from; now <= to; now += 16)
        {
            _ = app.Tick(now).ToList();
        }

        return to;
    }

    [Fact]
    public void ThePreviewShowsTheStarsTheGalaxyWillHave()
    {
        var app = AtGameSetup();
        var preview = app.Current.Window.Get<UniversePreviewPane>(10001);
        Assert.Equal(20, preview.Count);
        Assert.InRange(preview.Seed, 0u, 24999u);

        var seed = preview.Seed;
        Click(app, 160, 212);
        Assert.NotEqual(seed, preview.Seed);
        Assert.InRange(preview.Seed, 0u, 9999u);
    }

    [Fact]
    public void FleetAndFlagArrowsWrap()
    {
        var app = AtGameSetup();
        var window = app.Current.Window;
        var flag = window.Get<FlagPane>(10007);
        var previous = window.Get(10105);
        Click(app, previous.Left + 5, previous.Top + 5);
        Assert.Equal(29, flag.Flag);
    }

    [Fact]
    public void StartAsksTheHostForTheGameChosen()
    {
        var app = AtGameSetup();
        var requests = Click(app, 120, 470);

        var start = Assert.Single(requests);
        Assert.Equal(HostRequestKind.StartGame, start.Kind);
        var choices = start.Choices!;
        Assert.Equal(20, choices.Stars);
        Assert.Equal(15, choices.Opponents);
        Assert.Equal(3, choices.Difficulty);
        Assert.Equal(app.StockSpecies[0], choices.Species);
        Assert.Equal(app.Current.Window.Get<UniversePreviewPane>(10001).Seed, choices.Seed);
    }

    [Fact]
    public void TypingEditsTheEmpireName()
    {
        var app = AtGameSetup();
        var name = app.Current.Window.Get<TextEditPane>(10005);

        // The first click selects everything, so typing replaces it.
        Click(app, name.Left + 5, name.Top + 5);
        app.Typed("Ok", 0);
        Assert.Equal("Ok", name.Text);
        app.Key(UiKey.Backspace, false, 0);
        Assert.Equal("O", name.Text);
    }

    [Fact]
    public void EveryNewGameScreenDraws()
    {
        var app = AtGameSetup();
        var frame = new Framebuffer(640, 480);
        app.Draw(frame);
        Assert.Contains(frame.Pixels, p => p != 0);
    }
}
