using OpenPax.Sim.Players;
using OpenPax.Sim.Setup;

namespace OpenPax.Sim.Tests;

public class ShipyardTests
{
    [Fact]
    public void AQueuedShipIsBuiltIntoAFleetAtTheYard()
    {
        var tables = GameTablesForTests.Load();
        var game = NewGame.Create(new GameSetup(1234, 30, 2, 3, [new HumanPlayerSetup("Commander", 0, 0, tables.Species[0])]), tables);
        var human = game.Players[0];
        var colony = human.Colonies[0];
        var yard = colony.FirstOfKind(TechInfo.ShipyardKind)!;
        var ship = game.NewShip(human.Designs[0]!);
        Game.QueueShip(yard, ship);
        Assert.Equal(ship.BuildCost(), yard.Total);

        var periods = 0;
        while (yard.Queue.Count > 0 && periods < 500)
        {
            for (var i = 0; i < 40; i++)
            {
                game.Step();
            }

            periods++;
        }

        Assert.Empty(yard.Queue);
        Assert.NotNull(ship.Fleet);
        Assert.Same(colony.Planet, ship.Fleet!.OrbitCenter);
        Assert.Contains(ship.Fleet, human.Fleets);
    }
}
