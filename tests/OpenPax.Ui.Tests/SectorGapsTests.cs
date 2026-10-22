using OpenPax.Sim;
using OpenPax.Sim.Players;
using OpenPax.Ui.Screens;
using OpenPax.Ui.Widgets;
using static OpenPax.Ui.Tests.UiGames;

namespace OpenPax.Ui.Tests;

public class SectorGapsTests
{
    [Fact]
    public void ThePlanetPanelSaysWhatTheColonyIsWorkingOn()
    {
        var app = InGame();
        var session = app.Session!;
        var home = session.Local.Colonies[0];
        session.Select(home.Planet);
        app.Command(UiCommand.SectorDisplay);
        var info = app.Current.Window.Get<InfoPanelPane>(13104);
        var idle = info.WorkLine(session, home);
        Assert.True(idle is null || idle.EndsWith("% ", StringComparison.Ordinal));

        var slot = Array.FindIndex(session.Local.Designs, d => d is not null);
        session.Game.OrderShips(session.Local, home.Planet, slot, 2);
        var line = info.WorkLine(session, home)!; // a ship is now queued
        Assert.StartsWith("2 / ", line, StringComparison.Ordinal);
        Assert.Contains(session.Local.Designs[slot]!.ClassName, line, StringComparison.Ordinal);
        _ = app.Tick(10).ToList();
        app.Draw(new OpenPax.Render.Framebuffer(640, 480));
    }

    [Fact]
    public void ABattlesMarkerShowsInItsSystemAndADoubleClickOpensTheBattle()
    {
        var app = InGame();
        var session = app.Session!;
        var game = session.Game;
        var computer = game.Players.First(p => p != session.Local);
        var ours = session.Local.Fleets.First(f => f.Ships.Count > 0);
        var events = StepEvents.None;
        game.SetTreaty(session.Local, computer, Treaty.War, Treaty.InForce, ref events);
        game.SetTreaty(computer, session.Local, Treaty.War, Treaty.InForce, ref events);
        // Fleets take their place on the first star step; this one takes its orbit step now.
        ours.OrbitStep();
        Assert.NotEqual((0, 0), (ours.X, ours.Y));
        var (x, y) = (ours.X, ours.Y);
        game.StartFleetBattle(ours, computer.Fleets.First(f => f.Ships.Count > 0));
        var battle = game.Battles[0];
        Assert.Equal((x, y), battle.Marker);

        game.Observe(session.Local); // the system must have been seen
        session.SetCurrentStar(battle.Star);
        app.Command(UiCommand.SectorDisplay);
        var map = app.Current.Window.Get<MapPane>(13100);
        var (px, py) = map.ToSystemPane(x, y);
        _ = app.Tick(5).ToList();
        app.Draw(new OpenPax.Render.Framebuffer(640, 480));
        app.MouseDown(px, py, 2, 10);
        app.MouseUp(px, py);
        _ = app.Tick(20).ToList();
        Assert.Equal(ScreenMode.Combat, app.Mode);
    }
}
