using OpenPax.Sim.Galaxy;
using OpenPax.Sim.Players;
using OpenPax.Ui.Widgets;
using static OpenPax.Ui.Tests.UiGames;

namespace OpenPax.Ui.Tests;

public class SectorDisplayTests
{
    private const int SystemMap = 13100;
    private const int GalaxyMap = 13101;

    private static (UiApp App, GameSession Session, MapPane System) AtSectorDisplay()
    {
        var app = InGame();
        app.Command(UiCommand.SectorDisplay);
        return (app, app.Session!, app.Current.Window.Get<MapPane>(SystemMap));
    }

    private static void ClickAt(UiApp app, MapPane map, int px, int py, bool right = false)
    {
        var (x, y) = map.ToSystemPane(px, py);
        app.MouseDown(x, y, 1, 0, right);
        app.MouseUp(x, y);
        _ = app.Tick(0).ToList();
    }

    [Fact]
    public void ClickingAPlanetSelectsItAndShowsItsActions()
    {
        var (app, session, map) = AtSectorDisplay();
        var home = session.Local.Colonies[0].Planet;

        ClickAt(app, map, home.X, home.Y);
        Assert.Equal(home, session.Selection);
        Assert.True(app.Current.Window.Get(13200).Visible);
        Assert.False(app.Current.Window.Get(13220).Visible);
    }

    [Fact]
    public void ClickingOwnFleetShowsItsOrders()
    {
        var (app, session, map) = AtSectorDisplay();
        var fleet = session.Current.Fleets.First(f => f.Owner == session.Local);

        ClickAt(app, map, fleet.X, fleet.Y);
        Assert.Equal(fleet, session.Selection);
        var window = app.Current.Window;
        Assert.True(window.Get(13220).Visible);
        Assert.True(window.Get(13222).Visible);
        Assert.Equal(Sim.Game.ColonyShips(fleet) > 0, window.Get(13225).Visible);
        Assert.Equal(fleet, window.Get<FleetPanelPane>(13901).Fleet);
    }

    [Fact]
    public void TheRightButtonSendsTheSelectedFleetToAPoint()
    {
        var (app, session, map) = AtSectorDisplay();
        var fleet = session.Current.Fleets.First(f => f.Owner == session.Local);
        ClickAt(app, map, fleet.X, fleet.Y);

        // A point away from everything in the system.
        var free = Enumerable.Range(0, 20).Select(i => (X: 30 + (i * 19), Y: 410))
            .First(p => session.Current.Planets.All(pl => Math.Abs(pl.X - p.X) > 25 || Math.Abs(pl.Y - p.Y) > 25)
                && session.Current.Wormholes.All(w => Math.Abs(w.X - p.X) > 25 || Math.Abs(w.Y - p.Y) > 25)
                && session.Current.Fleets.All(f => Math.Abs(f.X - p.X) > 25 || Math.Abs(f.Y - p.Y) > 25));
        ClickAt(app, map, free.X, free.Y, right: true);

        var order = Assert.Single(fleet.Orders);
        Assert.Null(order.Target);
        Assert.Equal(fleet, session.Selection);
    }

    [Fact]
    public void MoveThenAStarOnTheGalaxyMapPlansTheRoute()
    {
        var (app, session, map) = AtSectorDisplay();
        var fleet = session.Current.Fleets.First(f => f.Owner == session.Local);
        ClickAt(app, map, fleet.X, fleet.Y);

        var move = app.Current.Window.Get(13222);
        Click(app, move.Left + 5, move.Top + 5);
        Assert.Equal(1, map.OrderMode);

        var neighbor = session.Current.Wormholes.First(w => w.Partner is not null).Partner!.Star;
        var galaxy = app.Current.Window.Get<MapPane>(GalaxyMap);
        var target = session.Game.Universe.Stars[neighbor];
        var (x, y) = galaxy.ToPane(target.X, target.Y);
        Click(app, x, y);

        Assert.Equal(0, map.OrderMode);
        var hop = Assert.IsType<Wormhole>(Assert.Single(fleet.Orders).Target);
        Assert.Equal(neighbor, hop.Partner!.Star);
    }

    [Fact]
    public void InterdictTogglesIntercepting()
    {
        var (app, session, map) = AtSectorDisplay();
        var fleet = session.Current.Fleets.First(f => f.Owner == session.Local);
        ClickAt(app, map, fleet.X, fleet.Y);

        var interdict = app.Current.Window.Get(13224);
        Click(app, interdict.Left + 5, interdict.Top + 5);
        Assert.True(fleet.Intercepting);
    }

    [Fact]
    public void ColonizeSendsAColonyShipToTheSelectedPlanet()
    {
        var (app, session, map) = AtSectorDisplay();
        var target = session.Current.Planets.FirstOrDefault(p => p.Colony is null && p.Class != 5);
        Assert.SkipWhen(target is null, "The home system has no free rocky planet with this seed.");
        Assert.SkipWhen(!Sim.Game.CanColonizeFrom(session.Current, session.Local), "No colony ship at home.");

        // A target never null past the skip above.
        ClickAt(app, map, target!.X, target.Y);
        var colonize = app.Current.Window.Get(13202);
        Assert.True(colonize.Visible);
        Click(app, colonize.Left + 5, colonize.Top + 5);

        var fleet = session.Current.Fleets.First(f => f.Owner == session.Local && f.Orders.Count > 0);
        Assert.Equal(target, fleet.Orders[^1].Target);
        Assert.Equal(FleetMode.Colonize, fleet.Mode);
    }

    [Fact]
    public void AFleetStaysSelectedWhenAnotherStarBecomesCurrent()
    {
        var (app, session, map) = AtSectorDisplay();
        var fleet = session.Current.Fleets.First(f => f.Owner == session.Local);
        ClickAt(app, map, fleet.X, fleet.Y);

        var galaxy = app.Current.Window.Get<MapPane>(GalaxyMap);
        var other = session.Game.Universe.Stars.First(s => s.Index != session.CurrentStar);
        var (x, y) = galaxy.ToPane(other.X, other.Y);
        Click(app, x, y);

        Assert.Equal(other.Index, session.CurrentStar);
        Assert.Equal(fleet, session.Selection);
    }
}
