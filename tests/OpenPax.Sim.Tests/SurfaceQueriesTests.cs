using OpenPax.Sim.Players;
using OpenPax.Sim.Setup;

namespace OpenPax.Sim.Tests;

public class SurfaceQueriesTests
{
    private static (Game Game, Player Human) NewHumanGame()
    {
        var tables = GameTablesForTests.Load();
        var game = NewGame.Create(new GameSetup(1234, 30, 2, 3, [new HumanPlayerSetup("Commander", 0, 0, tables.Species[0])]), tables);
        return (game, game.Players[0]);
    }

    [Fact]
    public void TheFirstUnfinishedImprovementIsWhatIsBuiltNext()
    {
        var (game, human) = NewHumanGame();
        var colony = human.Colonies[0];
        var list = game.UnfinishedImprovements(colony);
        Assert.Equal(game.NextImprovementId(colony), list.Count > 0 ? list[0].Tech.Id : -1);
        Assert.All(list, i => Assert.True(i.Percent < 100.0));
    }

    [Fact]
    public void FinishedImprovementsAreCountedByResource()
    {
        var (_, human) = NewHumanGame();
        var colony = human.Colonies[0];
        var expected = colony.Improvements.Count(i => i is { Percent: 100.0 });
        Assert.Equal(expected, Game.FinishedByResource(colony).Sum());
    }

    [Fact]
    public void TheSubsidyChangesTheColonyAndThePlayersTotal()
    {
        var (game, human) = NewHumanGame();
        var colony = human.Colonies[0];
        Game.ChangeFunding(human, colony.Planet, 40);
        Assert.Equal(40.0, colony.Funding);
        Assert.Equal(40.0, human.Funding);
        Game.ChangeFunding(human, colony.Planet, 10);
        Assert.Equal(10.0, colony.Funding);
        Assert.Equal(10.0, human.Funding);

        // Not the player's colony: nothing.
        Game.ChangeFunding(game.Players[1], colony.Planet, 99);
        Assert.Equal(10.0, colony.Funding);
    }
}
