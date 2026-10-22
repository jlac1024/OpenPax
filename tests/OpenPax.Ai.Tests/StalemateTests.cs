using OpenPax.Sim;
using OpenPax.Sim.Players;

namespace OpenPax.Ai.Tests;

public class StalemateTests
{
    [Fact]
    public void ABattleNobodyCanWinEndsWithEverySideGoingHome()
    {
        var game = AiGames.New(1234, 2, 3);
        var (a, b) = (game.Players[0], game.Players[1]);
        a.Treaties[b.Index].Write(Treaty.War, Treaty.InForce);
        b.Treaties[a.Index].Write(Treaty.War, Treaty.InForce);
        a.RelationScores[b.Index] = -200.0;
        b.RelationScores[a.Index] = -200.0;

        // Colony ships carry no weapons: neither side can hurt the other.
        var home = game.Universe.Stars[a.HomeStar];
        var planet = home.Planets.First(p => p.Owner == a);
        var mine = game.CreateFleet(a, home, planet);
        var theirs = game.CreateFleet(b, home, planet);
        mine.AddShip(game.NewShip(a.Designs[2]!), game.Tables.Tech);
        theirs.AddShip(game.NewShip(b.Designs[2]!), game.Tables.Tech);
        var caution = (a.Caution, b.Caution);

        game.StartFleetBattle(mine, theirs);
        game.Step();

        Assert.False(game.InCombat);
        Assert.Single(mine.Ships);
        Assert.Single(theirs.Ships);
        Assert.False(mine.InCombat || theirs.InCombat);
        Assert.Equal(caution.Item1 + 0.2, a.Caution, 9);
        Assert.Equal(caution.Item2 + 0.2, b.Caution, 9);
    }
}
