using OpenPax.Sim.Players;
using OpenPax.Ui.Screens;
using OpenPax.Ui.Widgets;
using static OpenPax.Ui.Tests.UiGames;

namespace OpenPax.Ui.Tests;

public class ShipDesignTests
{
    private static (UiApp App, GameSession Session, ShipDesignScreen Screen) AtShipDesign()
    {
        var app = InGame();
        app.Command(UiCommand.ShipDesign);
        Assert.Equal(ScreenMode.ShipDesign, app.Mode);
        return (app, app.Session!, (ShipDesignScreen)app.Current);
    }

    private static void Press(UiApp app, int id)
    {
        var window = app.Popup?.Window ?? app.Current.Window;
        var pane = window.Get(id);
        Assert.True(pane.Shown && pane.Enabled, $"{id} cannot be pressed");
        Click(app, pane.Left + 5, pane.Top + 5);
    }

    [Fact]
    public void BrowsingStepsThroughTheDesigns()
    {
        var (app, session, screen) = AtShipDesign();
        Assert.Same(session.Local.Designs[0], screen.Shown);
        Press(app, 26002);
        Assert.Same(session.Local.Designs[1], screen.Shown);
        Press(app, 26001);
        Press(app, 26001);
        var last = session.Local.Designs.Last(d => d is not null);
        Assert.Same(last, screen.Shown);

        app.Tick(100);
        app.Draw(new OpenPax.Render.Framebuffer(640, 480));
    }

    [Fact]
    public void ADesignIsBuiltAndAccepted()
    {
        var (app, session, screen) = AtShipDesign();
        Press(app, 26004);
        var design = screen.Editing!;
        Assert.Equal(session.Local.Designs[0]!.Type, design.Type);
        Assert.Equal("Unnamed", app.Current.Window.Get<TextEditPane>(26036).Text);

        // Another hull clears the design; then fit a tactical drive.
        Press(app, 26012);
        Press(app, 26211);
        Assert.Equal(1, screen.Editing!.Type);
        Assert.Null(screen.Editing!.TacticalDrive);
        Assert.False(app.Current.Window.Get(26007).Enabled);

        Press(app, 26014);
        var available = app.Current.Window.Get<ListBoxPane>(26033);
        Assert.NotEmpty(available.Rows);
        Press(app, 26009);
        Assert.NotNull(screen.Editing!.TacticalDrive);
        Assert.True(app.Current.Window.Get(26007).Enabled);

        var free = Array.IndexOf(session.Local.Designs, null);
        Press(app, 26007);
        Assert.Null(screen.Editing);
        Assert.Same(session.Local.Designs[free], screen.Shown);
        Assert.Equal("Unnamed", screen.Shown!.Name);
    }

    [Fact]
    public void WeaponsGoOnTheirArc()
    {
        var (app, _, screen) = AtShipDesign();
        Press(app, 26004);
        Press(app, 26016);
        Assert.Equal(1, app.Current.Window.Get<WeaponArcsPane>(26039).Highlighted);
        var available = app.Current.Window.Get<ListBoxPane>(26033);
        Assert.SkipWhen(available.Rows.Count == 0, "No weapon known with this seed.");

        var before = screen.Editing!.Weapons.Count(w => w.Mode == 1);
        Press(app, 26009);
        var fitted = app.Current.Window.Get<ListBoxPane>(26300);
        Assert.Contains(fitted.Rows.Cast<ComponentRow>(), r => r.Fitted?.Mode == 1);
        Press(app, 26010);
        Assert.Equal(before, screen.Editing!.Weapons.Count(w => w.Mode == 1));
    }

    [Fact]
    public void AutoFillsTheHullAndTheDesignSurvivesLeaving()
    {
        var (app, session, screen) = AtShipDesign();
        Press(app, 26004);
        Press(app, 26008);
        Assert.NotNull(screen.Editing!.TacticalDrive);

        app.Command(UiCommand.Bridge);
        app.Command(UiCommand.ShipDesign);
        var again = (ShipDesignScreen)app.Current;
        Assert.Same(session.DesignInProgress, again.Editing);
        Press(app, 26006);
        Assert.Null(session.DesignInProgress);
    }

    [Fact]
    public void DeleteAsksThenEmptiesTheSlot()
    {
        var (app, session, screen) = AtShipDesign();
        Press(app, 26002);
        var shown = screen.Shown!;
        Press(app, 26003);
        Assert.StartsWith("Are you sure you want to delete the ship design", app.Popup!.Window.Get<WrappedTextPane>(38503).Text);
        Press(app, 38501);
        Assert.Null(session.Local.Designs[shown.Slot]);
        Assert.NotSame(shown, screen.Shown);
    }

    [Fact]
    public void TheBridgeStationOpensIt()
    {
        var app = InGame();
        var station = app.Current.Window.Get(22005);
        Click(app, station.Left + 5, station.Top + 5);
        Assert.Equal(ScreenMode.ShipDesign, app.Mode);
    }

    [Fact]
    public void OnAGuestsGameAutoAndAcceptFinishWhenTheServerHasDoneThem()
    {
        var (app, session, screen) = AtShipDesign();
        var held = new List<OpenPax.Sim.Commands.Command>();
        session.Link = new RecordingLink(held);
        Press(app, 26004);
        var blank = screen.Editing;
        Press(app, 26008);

        // Nothing comes back until the server has applied AUTO.
        _ = app.Tick(0).ToList();
        Assert.Same(blank, screen.Editing);
        foreach (var c in held)
        {
            session.Game.Apply(c);
        }

        held.Clear();
        _ = app.Tick(0).ToList();
        Assert.NotSame(blank, screen.Editing);
        Assert.Equal(session.Local.Draft!.Cost, screen.Editing!.Cost); // Not null: AUTO was applied just above.

        var free = Array.IndexOf(session.Local.Designs, null);
        Press(app, 26007);
        _ = app.Tick(0).ToList();
        Assert.NotNull(screen.Editing);
        foreach (var c in held)
        {
            session.Game.Apply(c);
        }

        _ = app.Tick(0).ToList();
        Assert.Null(screen.Editing);
        Assert.Same(session.Local.Designs[free], screen.Shown);
    }
}
