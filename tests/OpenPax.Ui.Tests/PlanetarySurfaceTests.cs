using OpenPax.Sim.Galaxy;
using OpenPax.Ui.Screens;
using OpenPax.Ui.Widgets;
using static OpenPax.Ui.Tests.UiGames;

namespace OpenPax.Ui.Tests;

public class PlanetarySurfaceTests
{
    private static (UiApp App, GameSession Session, PlanetarySurfaceScreen Screen) AtHome()
    {
        var app = InGame();
        var session = app.Session!;
        session.Select(session.Local.Colonies[0].Planet);
        app.Command(UiCommand.PlanetarySurface);
        Assert.Equal(ScreenMode.PlanetarySurface, app.Mode);
        return (app, session, (PlanetarySurfaceScreen)app.Current);
    }

    private static void Press(UiApp app, int id)
    {
        var pane = app.Current.Window.Get(id);
        Assert.True(pane.Shown && pane.Enabled, $"{id} cannot be pressed");
        Click(app, pane.Left + 5, pane.Top + 5);
    }

    [Fact]
    public void TheSelectedPlanetIsShownWithItsWork()
    {
        var (app, session, screen) = AtHome();
        var home = session.Local.Colonies[0];
        Assert.Same(home.Planet, screen.Planet);
        var window = app.Current.Window;
        Assert.Equal(home.Planet.Name, window.Get<TextLinePane>(14004).Text);
        Assert.Equal(home.Planet.Class + 6, window.PaletteIndex);
        var rows = window.Get<ListBoxPane>(14101).Rows;
        Assert.Equal(session.Game.UnfinishedImprovements(home).Count + (home.FirstOfKind(OpenPax.Sim.Players.TechInfo.ShipyardKind) is { Queue.Count: > 0 } || OpenPax.Sim.Game.Damaged(home) is not null ? 1 : 0), rows.Count);
        Assert.NotEmpty(window.Get<TextLinePane>(14034).Text);
        Assert.StartsWith("$", window.Get<TextLinePane>(14013).Text);
        Assert.Equal(14034, window.Get<PicturePane>(14036).Picture);

        app.Tick(100);
        app.Tick(200);
        app.Draw(new OpenPax.Render.Framebuffer(640, 480));
    }

    [Fact]
    public void ThePlanArrowsStepThePlan()
    {
        var (app, session, _) = AtHome();
        var home = session.Local.Colonies[0];
        var plan = home.Plan;
        Press(app, 14033);
        Assert.Equal((plan + 1) % 6, home.Plan);
        Assert.Equal(app.Assets.Strings.Get(new OpenPax.Data.Views.TextRef(103, home.Plan)), app.Current.Window.Get<TextLinePane>(14034).Text);
    }

    [Fact]
    public void TheSubsidySpinnerFundsTheColony()
    {
        var (app, session, _) = AtHome();
        var spinner = app.Current.Window.Get<SpinnerPane>(14029);
        Click(app, spinner.Left + spinner.Width - 5, spinner.Top + 6);
        Assert.Equal(1, spinner.Value);
        Assert.Equal(1.0, session.Local.Colonies[0].Funding);
    }

    [Fact]
    public void ExtendGrowsTheQueueUpward()
    {
        var (app, session, _) = AtHome();

        // Two improvements learned give two lines of work; the window follows.
        var game = session.Game;
        foreach (var tech in game.Tables.OfKind(OpenPax.Sim.Players.TechInfo.ImprovementKind).Where(t => !session.Local.Knows(t.Id)).Take(2))
        {
            game.LearnTech(session.Local, tech);
        }

        app.Tick(0).ToList();

        var group = app.Current.Window.Get(14100);
        var bottom = group.Top + group.Height;
        Assert.SkipWhen(!app.Current.Window.Get(14031).Enabled, "Only one line of work with this seed.");
        Press(app, 14031);
        Assert.True(group.Height > 23);
        Assert.Equal(bottom, group.Top + group.Height);
        Press(app, 14031);
        Assert.Equal(23, group.Height);
    }

    [Fact]
    public void TheSectorDisplayAndTheBridgeOpenIt()
    {
        var app = InGame();
        var station = app.Current.Window.Get(22006);
        Click(app, station.Left + 5, station.Top + 5);
        Assert.Equal(ScreenMode.PlanetarySurface, app.Mode);

        app.Command(UiCommand.SectorDisplay);
        var session = app.Session!;
        var map = app.Current.Window.Get<MapPane>(13100);
        var home = session.Local.Colonies[0].Planet;
        session.SetCurrentStar(session.Game.Universe.Stars.First(s => s.Planets.Contains(home)).Index);
        var (x, y) = map.ToSystemPane(home.X, home.Y);
        Click(app, x, y, 0, 2);
        Assert.Equal(ScreenMode.PlanetarySurface, app.Mode);
        Assert.Same(home, ((PlanetarySurfaceScreen)app.Current).Planet);
    }
}
