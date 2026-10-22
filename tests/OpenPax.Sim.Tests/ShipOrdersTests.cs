using OpenPax.Sim.Galaxy;
using OpenPax.Sim.Players;
using OpenPax.Sim.Setup;

namespace OpenPax.Sim.Tests;

public class ShipOrdersTests
{
    private static (Game Game, Player Human) NewHumanGame()
    {
        var tables = GameTablesForTests.Load();
        var game = NewGame.Create(new GameSetup(1234, 30, 2, 3, [new HumanPlayerSetup("Commander", 0, 0, tables.Species[0])]), tables);
        return (game, game.Players[0]);
    }

    [Fact]
    public void ScrappingAShipTakesItAndItsUpkeepAway()
    {
        var (game, human) = NewHumanGame();
        var fleet = human.Fleets.First(f => f.Ships.Count > 0);
        var ship = fleet.Ships[0];
        var count = fleet.Ships.Count;
        var upkeep = human.Upkeep;

        game.ScrapShip(human, ship.ShipId);

        Assert.Equal(count - 1, fleet.Ships.Count);
        Assert.Null(ship.Fleet);
        Assert.Equal(upkeep - ship.Cost, human.Upkeep, 6);
        Assert.Null(game.FindShip(ship.ShipId));
    }

    [Fact]
    public void ShipsInBattleOrOfAnotherPlayerAreNotScrapped()
    {
        var (game, human) = NewHumanGame();
        var fleet = human.Fleets.First(f => f.Ships.Count > 0);
        var ship = fleet.Ships[0];

        game.ScrapShip(game.Players[1], ship.ShipId);
        Assert.Same(fleet, ship.Fleet);

        fleet.InCombat = true;
        game.ScrapShip(human, ship.ShipId);
        Assert.Same(fleet, ship.Fleet);
    }

    [Fact]
    public void ARefitMarkNeedsTheFleetAtAShipyard()
    {
        var (game, human) = NewHumanGame();
        var fleet = human.Fleets.First(f => f.Ships.Count > 0);
        var ship = fleet.Ships[0];
        var slot = human.Designs.OfType<Design>().First().Slot;

        Assert.True(Game.AtShipyard(fleet));
        game.MarkRefit(human, ship.ShipId, slot);
        Assert.NotNull(ship.Refit);
        Assert.NotEqual(ship.ShipId, ship.Refit!.ShipId);

        var other = fleet.Ships.Count > 1 ? fleet.Ships[1] : null;
        fleet.LeaveOrbit();
        Assert.False(Game.AtShipyard(fleet));
        if (other is not null)
        {
            game.MarkRefit(human, other.ShipId, slot);
            Assert.Null(other.Refit);
        }
    }

    [Fact]
    public void RefitChoicesAreNewerDesignsOfTheSameHull()
    {
        var (_, human) = NewHumanGame();
        var ship = human.Fleets.SelectMany(f => f.Ships).First();
        Assert.All(Game.RefitChoices(human, ship), d =>
        {
            Assert.Equal(ship.Type, d.Type);
            Assert.True(d.Id > ship.Id);
        });
        Assert.DoesNotContain(Game.RefitChoices(human, ship), d => d.Id == ship.Id);
    }

    [Fact]
    public void FleetConditionIsTheShipsAverageRoundedDown()
    {
        var (_, human) = NewHumanGame();
        var fleet = human.Fleets.First(f => f.Ships.Count > 0);
        fleet.Ships[0].Structure = fleet.Ships[0].MaxStructure / 2;
        var expected = (int)fleet.Ships.Average(s => (double)s.ConditionPercent());
        Assert.Equal(expected, Game.FleetCondition(fleet));
        Assert.IsType<Planet>(fleet.OrbitCenter);
    }
}
