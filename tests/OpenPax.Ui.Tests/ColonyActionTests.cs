using OpenPax.Sim.Galaxy;
using OpenPax.Sim.Players;
using OpenPax.Ui.Widgets;
using static OpenPax.Ui.Tests.UiGames;

namespace OpenPax.Ui.Tests;

public class ColonyActionTests
{
    private static (UiApp App, GameSession Session, Planet Home) AtHomePlanet()
    {
        var app = InGame();
        app.Command(UiCommand.SectorDisplay);
        var session = app.Session!;
        var home = session.Local.Colonies[0].Planet;
        var map = app.Current.Window.Get<MapPane>(13100);
        var (x, y) = map.ToSystemPane(home.X, home.Y);
        Click(app, x, y);
        return (app, session, home);
    }

    private static void Press(UiApp app, int id)
    {
        var window = app.Popup?.Window ?? app.Current.Window;
        var pane = window.Get(id);
        Assert.True(pane.Visible, $"{id} is not shown");
        Click(app, pane.Left + 5, pane.Top + 5);
    }

    [Fact]
    public void BuildAShipListsTheDesignsAndQueuesTheCountChosen()
    {
        var (app, session, home) = AtHomePlanet();
        Press(app, 13203);

        var window = app.Current.Window;
        Assert.True(window.Get(13920).Visible);
        var list = window.Get<ListBoxPane>(13922);
        Assert.Equal(session.Local.Designs.Count(d => d is not null), list.Rows.Count);
        var names = list.Rows.Cast<DesignRow>().Select(r => r.Design.ClassName).ToList();
        Assert.Equal(names.Order(StringComparer.Ordinal), names);

        // Pick the first row, ask for two, build.
        Click(app, list.Left + 10, list.Top + 5);
        var spinner = window.Get<SpinnerPane>(13926);
        spinner.Value = 2;
        Press(app, 13927);

        var yard = home.Colony!.FirstOfKind(TechInfo.ShipyardKind)!;
        Assert.Equal(2, yard.Queue.Count);
        Assert.Equal(((DesignRow)list.Rows[0]).Design.Slot, session.LastDesignSlot);
        Assert.True(app.Current.Window.Get(13200).Visible);
    }

    [Fact]
    public void StopTakesTheOldestShipOrderOff()
    {
        var (app, session, home) = AtHomePlanet();
        session.Game.OrderShips(session.Local, home, session.Local.Designs.First(d => d is not null)!.Slot, 1);
        session.Select(home);
        _ = app.Tick(0).ToList();

        Press(app, 13206);
        Assert.Empty(home.Colony!.FirstOfKind(TechInfo.ShipyardKind)!.Queue);
    }

    [Fact]
    public void BuildOrbitalAddsWhatTheButtonNames()
    {
        var (app, session, home) = AtHomePlanet();

        // At the start every stack is at its tech level; better techs open the panel.
        foreach (var tech in session.Game.Tables.Techs.Where(t => !session.Local.Knows(t.Id)))
        {
            session.Game.GiveTech(session.Local, tech);
        }

        session.Select(home);
        _ = app.Tick(0).ToList();
        Press(app, 13207);
        var window = app.Current.Window;
        Assert.True(window.Get(13230).Visible);

        int[] kinds = [9, 7, 8, 10, 6, 5];
        var button = Enumerable.Range(0, 6).First(i => window.Get(13231 + i).Visible);
        var kind = kinds[button];
        var before = home.Colony!.FirstOfKind(kind)?.Count ?? 0;
        Press(app, 13231 + button);
        Assert.Equal(before + 1, home.Colony.FirstOfKind(kind)?.Count ?? 0);
    }

    [Fact]
    public void ScrappingAsksFirstAndTakesOneUnit()
    {
        var (app, _, home) = AtHomePlanet();
        Press(app, 13205);

        int[] kinds = [9, 7, 8, 10, 6, 5];
        var window = app.Current.Window;
        var button = Enumerable.Range(0, 6).First(i => window.Get(13241 + i).Visible);
        var kind = kinds[button];
        var before = home.Colony!.FirstOfKind(kind)!.Count;
        Press(app, 13241 + button);

        Assert.NotNull(app.Popup);
        Assert.StartsWith("Are you sure you want to scrap", app.Popup!.Window.Get<WrappedTextPane>(38503).Text);
        Press(app, 38501);

        Assert.Null(app.Popup);
        Assert.Equal(before - 1, home.Colony.FirstOfKind(kind)?.Count ?? 0);
    }

    [Fact]
    public void NoLeavesTheColonyAlone()
    {
        var (app, _, home) = AtHomePlanet();
        Press(app, 13204);
        Assert.EndsWith(home.Name + "?", app.Popup!.Window.Get<WrappedTextPane>(38503).Text);
        Press(app, 38502);

        Assert.Null(app.Popup);
        Assert.NotNull(home.Colony);
    }

    [Fact]
    public void AbandoningDestroysTheColony()
    {
        var (app, session, home) = AtHomePlanet();
        Press(app, 13204);
        Press(app, 38501);

        Assert.Null(home.Colony);
        Assert.DoesNotContain(session.Local.Colonies, c => c.Planet == home);
    }
}
