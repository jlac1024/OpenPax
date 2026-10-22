using OpenPax.Sim.Galaxy;
using OpenPax.Ui.Screens;
using OpenPax.Ui.Widgets;
using static OpenPax.Ui.Tests.UiGames;

namespace OpenPax.Ui.Tests;

public class DomesticPolicyTests
{
    private static (UiApp App, GameSession Session) AtDomestic()
    {
        var app = InGame();
        app.Command(UiCommand.DomesticPolicy);
        Assert.Equal(ScreenMode.DomesticPolicy, app.Mode);
        return (app, app.Session!);
    }

    private static void Press(UiApp app, int id)
    {
        var pane = app.Current.Window.Get(id);
        Click(app, pane.Left + 5, pane.Top + 5);
    }

    [Fact]
    public void TheBudgetShowsTheYearlyFigures()
    {
        var (app, session) = AtDomestic();
        var window = app.Current.Window;
        Assert.Equal("$" + ((int)session.Local.Money).ToString("#,0", System.Globalization.CultureInfo.InvariantCulture), window.Get<TextLinePane>(17029).Text);
        Assert.StartsWith("$", window.Get<TextLinePane>(17028).Text);
        Assert.Equal(100, window.Get<SpinnerPane>(17015).Value);
    }

    [Fact]
    public void TheColumnsHoldTheOwnColonyAndOpenPlanets()
    {
        var (app, session) = AtDomestic();
        var list = app.Current.Window.Get<ListBoxPane>(17001);
        Assert.True(list.Horizontal);
        var columns = list.Rows.Cast<PlanetColumn>().ToList();
        Assert.Contains(columns, c => c.Planet == session.Local.Colonies[0].Planet);
        Assert.All(columns, c => Assert.True(c.Planet.Colony is null || c.Planet.Colony.Owner == session.Local || c.Star.Sight[session.Local.Index] != Star.InView));

        // Colonized off: only planets with no colony known stay.
        Press(app, 17006);
        Assert.False(session.DomesticFilters[3]);
        Assert.DoesNotContain(list.Rows.Cast<PlanetColumn>(), c => c.Planet == session.Local.Colonies[0].Planet);

        app.Tick(100);
        app.Draw(new OpenPax.Render.Framebuffer(640, 480));
    }

    [Fact]
    public void PickingTheHomeColumnSelectsItAndStepsThePlan()
    {
        var (app, session) = AtDomestic();
        var list = app.Current.Window.Get<ListBoxPane>(17001);
        var home = session.Local.Colonies[0];
        var index = list.Rows.Cast<PlanetColumn>().ToList().FindIndex(c => c.Planet == home.Planet);
        list.SetFraction(0.0);
        while (list.Rows.Take(index).Sum(r => r.Width) - list.Scroll >= list.Width)
        {
            list.Step(1);
        }

        var x = list.Left + list.Rows.Take(index).Sum(r => r.Width) - list.Scroll + 40;
        Click(app, x, list.Top + 100);
        Assert.Same(home.Planet, session.Selection);
        Assert.True(app.Current.Window.Get(17040).Enabled);

        var plan = home.Plan;
        Press(app, 17040);
        Assert.Equal((plan + 1) % 6, home.Plan);
    }

    [Fact]
    public void ASpinnerSetsTheSpending()
    {
        var (app, session) = AtDomestic();
        var spinner = app.Current.Window.Get<SpinnerPane>(17016);
        Click(app, spinner.Left + 5, spinner.Top + 6);
        Assert.Equal(99, spinner.Value);
        Assert.Equal(99.0, session.Local.Spending[1]);
    }

    [Fact]
    public void TheBridgeStationOpensIt()
    {
        var app = InGame();
        Press(app, 22004);
        Assert.Equal(ScreenMode.DomesticPolicy, app.Mode);
    }
}
