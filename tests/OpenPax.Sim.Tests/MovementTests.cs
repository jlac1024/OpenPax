using OpenPax.Sim.Galaxy;
using OpenPax.Sim.Players;
using OpenPax.Sim.Setup;

namespace OpenPax.Sim.Tests;

public class MovementTests
{
    private static Game NewGame(int players = 2)
    {
        var tables = GameTablesForTests.Load();
        var setup = new GameSetup(1234, 30, players, 3, [new HumanPlayerSetup("Commander", 0, 0, tables.Species[0])]);
        return Setup.NewGame.Create(setup, tables);
    }

    private static Fleet Scout(Game game)
    {
        return game.Players[0].Fleets.First(f => f.Ships.Count == 1 && f.Ships[0].Type == 0);
    }

    [Fact]
    public void TheDirectionTablesComeFromTheExecutable()
    {
        var tables = GameTablesForTests.Load();
        Assert.Equal(0.0349, tables.Sin[2]);
        Assert.Equal(1.0, tables.Cos[0]);
        Assert.Equal(360, tables.Sin.Count);
    }

    [Fact]
    public void AHumanFleetCirclesItsHomeworldEveryTick()
    {
        var game = NewGame();
        var fleet = Scout(game);
        var planet = (Planet)fleet.OrbitCenter!;
        var angle = fleet.OrbitAngle;

        game.Step();

        var expected = Orbit.Place(planet.X, planet.Y, Fleet.OrbitRadius, Orbit.Wrap(angle + fleet.OrbitSpeed));
        Assert.Equal(expected, (fleet.X, fleet.Y));
    }

    [Fact]
    public void AComputerFleetSitsOnItsHomeworld()
    {
        var game = NewGame();
        var ai = game.Players[1];
        var home = game.Universe.Stars[ai.HomeStar].Planets.First(p => p.Owner == ai);
        var start = (home.X, home.Y);
        var fleet = ai.Fleets[0];
        Assert.Null(fleet.OrbitCenter);
        game.Step();
        Assert.Equal(start, (fleet.X, fleet.Y));
    }

    [Fact]
    public void AFleetSentToAPointGetsThereAtItsSpeedAndStops()
    {
        var game = NewGame();
        var fleet = Scout(game);
        game.GiveOrder(fleet, Order.ToPoint(fleet.X + 10, fleet.Y));
        Assert.Null(fleet.OrbitCenter);
        var startX = fleet.X;

        game.Step();
        Assert.Equal(startX + 2, fleet.X);
        for (var i = 0; i < 4; i++)
        {
            game.Step();
        }

        Assert.Equal(startX + 10, fleet.X);
        Assert.Empty(fleet.Orders);
        game.Step();
        Assert.Equal(startX + 10, fleet.X);
    }

    [Fact]
    public void AnOrderToTheSamePlaceAsTheLastIsDropped()
    {
        var game = NewGame();
        var fleet = Scout(game);
        game.GiveOrder(fleet, Order.ToPoint(100, 100));
        game.GiveOrder(fleet, Order.ToPoint(120, 100));
        game.GiveOrder(fleet, Order.ToPoint(120, 100));
        Assert.Equal(2, fleet.Orders.Count);
    }

    [Fact]
    public void AWormholeJumpMovesTheFleetToThePartnersSystem()
    {
        var game = NewGame();
        var fleet = Scout(game);
        var home = game.Universe.Stars[fleet.Star];
        var hole = home.Wormholes[0];
        var partner = hole.Partner!;

        game.GiveOrder(fleet, Order.To(hole));
        var draws = game.Random.Draws;
        for (var i = 0; i < 1000 && fleet.Star == home.Index; i++)
        {
            game.Step();
        }

        Assert.Equal(partner.Star, fleet.Star);
        Assert.Contains(fleet, game.Universe.Stars[partner.Star].Fleets);
        Assert.DoesNotContain(fleet, home.Fleets);
        Assert.Same(partner, fleet.OrbitCenter);
        Assert.True(hole.Known && partner.Known);
        Assert.Equal(1, fleet.JumpsUsed);
        Assert.Equal(1, fleet.JumpsLeft);

        // Entering the new orbit draws an angle and a direction.
        Assert.Equal(draws + 2, game.Random.Draws);
    }

    [Fact]
    public void WithNoJumpsLeftAFleetStopsAtTheWormhole()
    {
        var game = NewGame();
        var fleet = Scout(game);
        for (var jump = 0; jump < 3; jump++)
        {
            var star = game.Universe.Stars[fleet.Star];
            var hole = star.Wormholes.First(w => w.Partner is not null && w.Partner.Star != star.Index);
            game.GiveOrder(fleet, Order.To(hole));
            var start = fleet.Star;
            for (var i = 0; i < 1000 && fleet.Star == start && fleet.Orders.Count > 0; i++)
            {
                game.Step();
            }

            if (jump < 2)
            {
                Assert.NotEqual(start, fleet.Star);
            }
            else
            {
                // Two jumps away from supply used the drive's range.
                Assert.Equal(start, fleet.Star);
                Assert.Same(hole, fleet.OrbitCenter);
                Assert.Empty(fleet.Orders);
            }
        }
    }

    [Fact]
    public void FleetsOfOneOwnerMerge()
    {
        var game = NewGame();
        var human = game.Players[0];
        var scout = Scout(game);
        var colony = human.Fleets.First(f => f != scout && f.Ships.Count > 0);
        var ships = scout.Ships.Count + colony.Ships.Count;

        game.GiveOrder(scout, Order.To(colony));
        for (var i = 0; i < 200 && human.Fleets.Contains(scout); i++)
        {
            game.Step();
        }

        Assert.DoesNotContain(scout, human.Fleets);
        Assert.Equal(ships, colony.Ships.Count);
        Assert.All(colony.Ships, s => Assert.Same(colony, s.Fleet));
    }

    [Fact]
    public void ReplaysWithOrdersAreIdentical()
    {
        ulong Run()
        {
            var game = NewGame(4);
            var fleet = Scout(game);
            game.GiveOrder(fleet, Order.To(game.Universe.Stars[fleet.Star].Wormholes[0]));
            for (var i = 0; i < 2000; i++)
            {
                game.Step();
            }

            return game.Hash();
        }

        Assert.Equal(Run(), Run());
    }
}
