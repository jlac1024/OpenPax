using OpenPax.Sim.Players;
using OpenPax.Sim.Setup;

namespace OpenPax.Sim.Tests;

public class AbandonTests
{
    [Fact]
    public void ANomadAbandonFleetTakesTwentyOneShipsAndTheRestAreRefused()
    {
        var tables = GameTablesForTests.Load();
        // No stock species is nomadic; the first one becomes so here.
        var stock = tables.Species[0];
        var nomads = stock with { CulturalTraits = [.. stock.CulturalTraits.Select((t, i) => t || i == Species.Nomad)] };
        var game = NewGame.Create(new GameSetup(1234, 30, 3, 3, [new HumanPlayerSetup("Commander", 0, 0, nomads)]), tables);
        var human = game.Players[0];
        var colony = human.Colonies[0];
        colony.Resources[Colony.Population] = 300.0;
        _ = game.TakeNotices(human);
        var fleets = human.Fleets.Count;

        game.Abandon(colony.Planet);
        var fleet = human.Fleets[0];
        Assert.Equal(fleets + 1, human.Fleets.Count);
        Assert.Equal(Fleet.MaxShips, fleet.Ships.Count);

        // Every ship made is announced, kept or not: 300 / 10 + 1.
        Assert.Equal(31, game.TakeNotices(human).Count(n => n.Kind == NoticeKind.ShipFinished));
    }
}
