using OpenPax.Sim.Players;
using OpenPax.Sim.Setup;

namespace OpenPax.Sim.Tests;

public class SplitFleetTests
{
    [Fact]
    public void TheShipsNamedLeaveForANewFleetInTheSamePlace()
    {
        var tables = GameTablesForTests.Load();
        var game = NewGame.Create(new GameSetup(1234, 30, 3, 3, [new HumanPlayerSetup("Commander", 0, 0, tables.Species[0])]), tables);
        var human = game.Players[0];
        var fleet = human.Fleets.First(f => f.Ships.Count > 0);
        fleet.AddShip(game.NewShip(fleet.Ships[0]), tables.Tech);
        fleet.AddShip(game.NewShip(fleet.Ships[0]), tables.Tech);
        fleet.Intercepting = true;
        var leaving = fleet.Ships[^1];
        var count = fleet.Ships.Count;
        var draws = game.Random.State;

        var copy = game.SplitFleet(fleet, [leaving.ShipId])!; // a ship was named, so a fleet was made
        Assert.Equal(draws, game.Random.State);
        Assert.Equal([leaving], copy.Ships);
        Assert.Equal(count - 1, fleet.Ships.Count);
        Assert.DoesNotContain(leaving, fleet.Ships);
        Assert.Equal((fleet.Star, fleet.OrbitCenter, fleet.OrbitAngle), (copy.Star, copy.OrbitCenter, copy.OrbitAngle));
        Assert.True(copy.Intercepting);
        Assert.Same(copy, human.Fleets[0]);
        Assert.Same(copy, game.Universe.Stars[fleet.Star].Fleets[0]);
        Assert.Null(game.SplitFleet(fleet, [leaving.ShipId]));
    }
}
