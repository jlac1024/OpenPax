using OpenPax.Sim.Setup;

namespace OpenPax.Sim.Tests;

public class PlayerEventTests
{
    [Fact]
    public void EachHumanGetsTheirOwnEventsAndNotices()
    {
        var tables = GameTablesForTests.Load();
        var game = NewGame.Create(new GameSetup(555, 30, 3, 3,
            [new HumanPlayerSetup("One", 0, 0, tables.Species[0]), new HumanPlayerSetup("Two", 1, 1, tables.Species[1])]), tables);
        var (one, two) = (game.Players[0], game.Players[1]);
        Assert.False(one.IsAi);
        Assert.False(two.IsAi);

        game.ApplyCheat(two, Cheat.KillPlayer);
        Assert.True(game.TakeEvents(two).HasFlag(StepEvents.Defeat));
        Assert.True(game.TakeEvents(one).HasFlag(StepEvents.PlayerEliminated));
        Assert.Contains(game.TakeNotices(one), n => n.Kind == NoticeKind.PlayerEliminated && n.Other == two);
        Assert.DoesNotContain(game.TakeNotices(two), n => n.Kind == NoticeKind.PlayerEliminated);

        // Taken once.
        Assert.Equal(StepEvents.None, game.TakeEvents(one));
    }
}
