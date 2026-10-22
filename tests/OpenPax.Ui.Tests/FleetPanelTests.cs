using OpenPax.Sim.Players;
using OpenPax.Ui.Widgets;
using static OpenPax.Ui.Tests.UiGames;

namespace OpenPax.Ui.Tests;

public class FleetPanelTests
{
    [Fact]
    public void UnselectedShipsAreSplitOffBeforeTheFleetsOrder()
    {
        var app = InGame();
        var session = app.Session!;
        var fleet = session.Local.Fleets.First(f => f.Ships.Count > 0);
        fleet.AddShip(session.Game.NewShip(fleet.Ships[0]), session.Game.Tables.Tech);
        fleet.OrbitStep();
        session.SetCurrentStar(fleet.Star);
        session.Select(fleet);
        app.Command(UiCommand.SectorDisplay);
        var panel = app.Current.Window.Get<FleetPanelPane>(13901);
        Assert.Same(fleet, panel.Fleet);

        // The first cell's ship is left out; the last selected one cannot be.
        Click(app, panel.Left + 10, panel.Top + 10);
        Assert.Single(session.LeftOut);
        Click(app, panel.Left + 52, panel.Top + 10);
        Assert.Single(session.LeftOut);
        var leftOut = session.LeftOut.Single();
        _ = app.Tick(10).ToList();
        app.Draw(new OpenPax.Render.Framebuffer(640, 480));

        // A move order to a planet of the system.
        var planet = session.Current.Planets.First(p => p.Class != 5);
        var map = app.Current.Window.Get<MapPane>(13100);
        var (x, y) = map.ToSystemPane(planet.X, planet.Y);
        app.MouseDown(x, y, 1, 20, right: true);
        app.MouseUp(x, y);
        _ = app.Tick(30).ToList();

        Assert.DoesNotContain(fleet.Ships, s => s.ShipId == leftOut);
        Assert.NotEmpty(fleet.Orders);
        var split = session.Local.Fleets.Single(f => f.Ships.Any(s => s.ShipId == leftOut));
        Assert.Empty(split.Orders);
        Assert.Empty(session.LeftOut);
    }
}
