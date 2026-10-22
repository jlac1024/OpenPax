using OpenPax.Sim.Galaxy;
using OpenPax.Sim.Players;
using OpenPax.Ui.Screens;
using OpenPax.Ui.Widgets;
using static OpenPax.Ui.Tests.UiGames;

namespace OpenPax.Ui.Tests;

public class FleetCommandTests
{
    private static (UiApp App, GameSession Session) AtFleetCommand()
    {
        var app = InGame();
        app.Command(UiCommand.FleetCommand);
        Assert.Equal(ScreenMode.FleetCommand, app.Mode);
        return (app, app.Session!);
    }

    private static void Press(UiApp app, int id)
    {
        var window = app.Popup?.Window ?? app.Current.Window;
        var pane = window.Get(id);
        Assert.True(pane.Shown && pane.Enabled, $"{id} cannot be pressed");
        Click(app, pane.Left + 5, pane.Top + 5);
    }

    [Fact]
    public void TheListHoldsTheOwnFleetsAndPicksTheFirst()
    {
        var (app, session) = AtFleetCommand();
        var list = app.Current.Window.Get<ListBoxPane>(20041);
        Assert.Equal(session.Local.Fleets.Count, list.Rows.Count);
        var fleet = ((FleetRow)list.Selected!).Fleet;
        Assert.Same(fleet, session.Selection);
        Assert.Equal(fleet.Star, session.CurrentStar);
        Assert.Equal(fleet.Name, app.Current.Window.Get<TextLinePane>(20048).Text);

        var ships = app.Current.Window.Get<ListBoxPane>(20043);
        Assert.Equal(fleet.Ships.Count, ships.Rows.Count);
        var preview = app.Current.Window.Get<DesignPreviewPane>(20047);
        Assert.Equal(0, preview.Mode);
        Assert.Same(((ShipRow)ships.Selected!).Ship, preview.Design);

        // Everything draws, the row icons included.
        app.Tick(100);
        app.Draw(new OpenPax.Render.Framebuffer(640, 480));
    }

    [Fact]
    public void TheSelectedFleetIsPickedOnOpening()
    {
        var app = InGame();
        var session = app.Session!;
        var fleet = session.Local.Fleets[^1];
        session.Select(fleet);
        app.Command(UiCommand.FleetCommand);
        Assert.Same(fleet, ((FleetRow)app.Current.Window.Get<ListBoxPane>(20041).Selected!).Fleet);
    }

    [Fact]
    public void ScrapAsksThenTakesTheShip()
    {
        var (app, session) = AtFleetCommand();
        var fleet = (Fleet)session.Selection!;
        var ships = app.Current.Window.Get<ListBoxPane>(20043);
        Assert.SkipWhen(session.Local.Fleets.Count == 1 && fleet.Ships.Count == 1, "Only one ship with this seed.");
        var ship = ((ShipRow)ships.Selected!).Ship;
        var count = fleet.Ships.Count;

        Press(app, 20004);
        Assert.StartsWith("Are you sure you want to scrap the ship", app.Popup!.Window.Get<WrappedTextPane>(38503).Text);
        Press(app, 38501);

        Assert.Null(app.Popup);
        Assert.Equal(count - 1, fleet.Ships.Count);
        Assert.DoesNotContain(ships.Rows.Cast<ShipRow>(), r => r.Ship == ship);
    }

    [Fact]
    public void TheShipyardPageBuildsTheChosenDesign()
    {
        var (app, session) = AtFleetCommand();
        Press(app, 20009);
        Assert.True(session.FleetShowsYards);

        var window = app.Current.Window;
        var yards = window.Get<ListBoxPane>(20021);
        Assert.Equal(session.Local.Colonies.Count(c => c.FirstOfKind(TechInfo.ShipyardKind) is not null), yards.Rows.Count);
        var row = (YardRow)yards.Selected!;
        Assert.IsType<Planet>(session.Selection);
        Assert.Equal(1, window.Get<DesignPreviewPane>(20047).Mode);
        Assert.All(window.Get<ListBoxPane>(20043).Rows, r => Assert.IsType<DesignRow>(r));

        Assert.SkipWhen(row.Yard.Queue.Count > 0 || row.Yard.Percent < 100.0, "The yard is busy with this seed.");
        Press(app, 20006);
        Assert.Single(row.Yard.Queue);
        Assert.True(window.Get(20007).Enabled);
        Assert.False(window.Get(20006).Enabled);

        Press(app, 20007);
        Assert.Empty(row.Yard.Queue);
    }

    [Fact]
    public void UpgradeListsTheFleetsShipsAndMarksOne()
    {
        var (app, session) = AtFleetCommand();
        var fleet = (Fleet)session.Selection!;
        Assert.SkipWhen(!Sim.Game.AtShipyard(fleet), "The first fleet is not at a shipyard with this seed.");

        Press(app, 20005);
        var popup = Assert.IsType<UpgradePopup>(app.Popup);
        var ships = popup.Window.Get<ListBoxPane>(20703);
        Assert.Equal(fleet.Ships.Count(s => s.Refit is null), ships.Rows.Count);
        Assert.Equal(fleet.Name, popup.Window.Get<TextLinePane>(20707).Text);

        var designs = popup.Window.Get<ListBoxPane>(20704);
        var ship = ((ShipRow)ships.Selected!).Ship;
        if (designs.Selected is RefitRow choice)
        {
            Press(app, 20701);
            Assert.Equal(choice.Design.Name, ship.Refit?.Name);
            Assert.DoesNotContain(ships.Rows.Cast<ShipRow>(), r => r.Ship == ship);
        }

        Press(app, 20702);
        Assert.Null(app.Popup);
    }

    [Fact]
    public void TheBridgeStationAndTheSectorDisplayOpenIt()
    {
        var app = InGame();
        var station = app.Current.Window.Get(22001);
        Click(app, station.Left + 5, station.Top + 5);
        Assert.Equal(ScreenMode.FleetCommand, app.Mode);

        app.Command(UiCommand.SectorDisplay);
        var session = app.Session!;
        session.Select(session.Local.Fleets[0]);
        app.Tick(0).ToList();
        Press(app, 13221);
        Assert.Equal(ScreenMode.FleetCommand, app.Mode);
    }

    [Fact]
    public void ActivatingAFleetOpensTheSectorDisplay()
    {
        var (app, session) = AtFleetCommand();
        var list = app.Current.Window.Get<ListBoxPane>(20041);
        var fleet = ((FleetRow)list.Rows[0]).Fleet;
        Click(app, list.Left + 30, list.Top + 5, 0, 2);
        Assert.Equal(ScreenMode.SectorDisplay, app.Mode);
        Assert.Same(fleet, session.Selection);
    }
}
