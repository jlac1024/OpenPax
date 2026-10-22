using OpenPax.Sim.Players;

namespace OpenPax.Ai.Tests;

public class BuildingTests
{
    [Fact]
    public void ComputerPlayersOrderDefensesAndKeepFundingAndPercentsSet()
    {
        var game = AiGames.New();

        // The starting missile base is level 1, so its stack cannot grow;
        // a level 2 missile base lets a computer player add a second unit.
        var p0 = game.Players[0];
        var better = game.Tables.OfKind(TechInfo.MissileBaseKind).First(t => t.InstallationLevel >= 2);
        game.GiveTech(p0, better);

        AiGames.Run(game, 400);

        // The homeworld is the oldest colony; new ones go to the front.
        Assert.Equal(2, p0.Colonies[^1].FirstOfKind(TechInfo.MissileBaseKind)!.Count);
        foreach (var p in game.Players)
        {
            // Espionage only once someone has been met (colonizing next to them); construction always 100.
            var met = game.Players.Any(o => o != p && p.Treaties[o.Index].Level() != Treaty.None);
            Assert.Equal(met ? 100.0 : 0.0, p.Spending[1]);
            Assert.Equal(100.0, p.Spending[2]);
            Assert.Equal(p.Colonies.Sum(c => c.Funding), p.Funding, 9);
        }
    }

    [Fact]
    public void NoKindPassesItsLimit()
    {
        var game = AiGames.New(difficulty: 5);
        AiGames.Run(game, 8000);
        foreach (var c in game.Players.SelectMany(p => p.Colonies))
        {
            Assert.All(c.Structures, s => Assert.InRange(s.Count, 1, Structure.MaxCount));
            Assert.Equal(c.Structures.Count, c.Structures.Select(s => s.Tech.Kind).Distinct().Count());
        }
    }

    [Fact]
    public void AiGamesAreDeterministic()
    {
        ulong Run()
        {
            var game = AiGames.New(99, 5, 4);
            AiGames.Run(game, 6000);
            return game.Hash();
        }

        Assert.Equal(Run(), Run());
    }
}
