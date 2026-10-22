namespace OpenPax.Ai.Tests;

public class ExpansionTests
{
    [Fact]
    public void ComputerPlayersBuildColonyShipsAndSettle()
    {
        var game = AiGames.New(1234, 4, 3);
        AiGames.Run(game, 20000);

        var colonies = game.Players.Sum(p => p.Colonies.Count);
        Assert.True(colonies > game.Players.Count, $"only {colonies} colonies after 20000 ticks");
        foreach (var p in game.Players)
        {
            Assert.All(p.Colonies, c => Assert.Same(p, c.Planet.Owner));
        }
    }

    [Fact]
    public void ExpandingGamesAreDeterministic()
    {
        ulong Run()
        {
            var game = AiGames.New(7, 6, 5);
            AiGames.Run(game, 12000);
            return game.Hash();
        }

        Assert.Equal(Run(), Run());
    }
}
