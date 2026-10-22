using OpenPax.Sim.Galaxy;
using OpenPax.Ui.Widgets;
using static OpenPax.Ui.Tests.UiGames;

namespace OpenPax.Ui.Tests;

/// <summary>The Sector Display's planet list (InfoPanelPane) with a star selected.</summary>
public sealed class PlanetListTests
{
    [Fact]
    public void AClickOnAPlanetsRowSelectsThePlanet()
    {
        var app = InGame();
        app.Command(UiCommand.SectorDisplay);
        var session = app.Session!;
        var star = session.Game.Universe.Stars[session.CurrentStar];
        session.Select(star);
        var list = app.Current.Window.Get<InfoPanelPane>(13104);
        var k = star.Planets.FindIndex(p => p.Class != 5);

        Click(app, list.Left + 60, list.Top + (66 * k) + 30);

        Assert.Equal(star.Planets[k], session.Selection);
    }
}
