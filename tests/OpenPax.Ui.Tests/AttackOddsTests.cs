using OpenPax.Render;
using OpenPax.Sim.Galaxy;
using OpenPax.Sim.Players;
using OpenPax.Ui.Widgets;
using static OpenPax.Ui.Tests.UiGames;

namespace OpenPax.Ui.Tests;

/// <summary>The chance a mod gives an attack, beside the pointer with ATTACK waiting (MapPane).</summary>
public sealed class AttackOddsTests
{
    [Fact]
    public void TheChanceShowsOverAnotherPlayersPlanetOrFleetOnlyWhenAModGivesIt()
    {
        var app = InGame();
        app.Command(UiCommand.SectorDisplay);
        var session = app.Session!;
        var game = session.Game;
        var enemy = game.Players.First(p => p != session.Local).Colonies[0];
        var star = game.Universe.Stars[enemy.Star];
        var fleet = game.CreateFleet(session.Local, star, enemy.Planet);
        fleet.AddShip(game.NewShip(session.Local.Fleets.SelectMany(f => f.Ships).First()), game.Tables.Tech);
        fleet.LeaveOrbit();
        fleet.MoveTo(enemy.Planet.X > 1000 ? 100 : 1900, 100); // away from the planet, so the pointer finds the planet
        session.SetCurrentStar(star.Index);
        session.Select(fleet);
        var map = app.Current.Window.Get<MapPane>(13100);
        var (x, y) = map.System.ToPane(enemy.Planet.X, enemy.Planet.Y);
        app.MouseMove(x, y, 0);
        _ = app.Tick(10).ToList(); // the pane under the pointer hears it once a frame
        map.OrderMode = 2; // ATTACK pressed, after the screen has taken in the new selection

        byte[] Frame()
        {
            var frame = new Framebuffer(640, 480) { Palette = app.Assets.Palette(app.PaletteIndex) };
            app.Draw(frame);
            return frame.Pixels;
        }

        var plain = Frame();
        Assert.Equal(plain, Frame());
        game.RateAttack = (_, _, _) => 0.42; // the pointer is on the planet or the computer's fleet sitting on it
        Assert.NotEqual(plain, Frame());
    }
}
