using OpenPax.Sim;
using OpenPax.Sim.Players;

namespace OpenPax.Ai.Tests;

public class CombatTests
{
    private static (Game Game, Player A, Player B) AtWar(uint seed = 1234)
    {
        var game = AiGames.New(seed, 2, 3);
        var (a, b) = (game.Players[0], game.Players[1]);
        a.Treaties[b.Index].Write(Treaty.War, Treaty.InForce);
        b.Treaties[a.Index].Write(Treaty.War, Treaty.InForce);
        a.RelationScores[b.Index] = -200.0;
        b.RelationScores[a.Index] = -200.0;
        return (game, a, b);
    }

    [Fact]
    public void AFleetBattleBetweenComputerPlayersResolvesInOneStep()
    {
        var (game, a, b) = AtWar();
        var home = game.Universe.Stars[a.HomeStar];
        var planet = home.Planets.First(p => p.Owner == a);

        // Two warfleets meet in a's home system.
        var mine = game.CreateFleet(a, home, planet);
        var theirs = game.CreateFleet(b, home, planet);
        for (var i = 0; i < 3; i++)
        {
            mine.AddShip(game.NewShip(a.Designs[1]!), game.Tables.Tech);
            theirs.AddShip(game.NewShip(b.Designs[1]!), game.Tables.Tech);
        }

        var before = mine.Ships.Sum(s => s.Structure) + theirs.Ships.Sum(s => s.Structure);
        game.StartFleetBattle(mine, theirs);
        Assert.True(game.InCombat);
        Assert.True(mine.InCombat && theirs.InCombat);

        var tick = game.Tick;
        game.Step();

        Assert.False(game.InCombat);
        Assert.Equal(tick, game.Tick);
        // A fleet left with no ships stays held until the star step removes it, as in the original.
        Assert.All(new[] { mine, theirs }.Where(f => f.Ships.Count > 0), f => Assert.False(f.InCombat));
        var after = mine.Ships.Sum(s => s.Structure) + theirs.Ships.Sum(s => s.Structure);
        Assert.True(after < before, "no damage was done");
        Assert.All(mine.Ships.Concat(theirs.Ships), s => Assert.False(s.InBattle));
    }

    [Fact]
    public void ComputerWarsAreFoughtAndStayDeterministic()
    {
        (ulong Hash, int Battles) Run()
        {
            var (game, _, _) = AtWar(7);
            var battles = 0;
            for (var i = 0; i < 30000; i++)
            {
                if (game.InCombat)
                {
                    battles++;
                }

                game.Step();
            }

            return (game.Hash(), battles);
        }

        var first = Run();
        Assert.True(first.Battles > 0, "no battle was fought");
        Assert.Equal(first, Run());
    }
}
