using OpenPax.Sim;
using OpenPax.Sim.Galaxy;
using OpenPax.Sim.Players;

namespace OpenPax.Ai.Tests;

public class SightTests
{
    [Fact]
    public void PlayersSeeTheirHomeSystemsFromTheStart()
    {
        var game = AiGames.New();
        Assert.All(game.Players, p => Assert.Equal(Star.InView, game.Universe.Stars[p.HomeStar].Sight[p.Index]));
    }

    [Fact]
    public void PlayersMeetWhenTheyShareASystem()
    {
        var game = AiGames.New(1234, 2, 3);
        var (a, b) = (game.Players[0], game.Players[1]);
        Assert.Equal(Treaty.None, a.Treaties[b.Index].Level());

        var home = game.Universe.Stars[a.HomeStar];
        game.CreateFleet(b, home, home.Planets.First(p => p.Owner == a));

        Assert.Equal(Treaty.InForce, a.Treaties[b.Index].Slot(Treaty.Neutral));
        Assert.Equal(Treaty.InForce, b.Treaties[a.Index].Slot(Treaty.Neutral));
        Assert.Equal(Star.InView, home.Sight[b.Index]);
    }

    [Fact]
    public void ASystemLeftBehindIsRemembered()
    {
        var game = AiGames.New(1234, 2, 3);
        var (a, b) = (game.Players[0], game.Players[1]);
        var home = game.Universe.Stars[a.HomeStar];

        // An empty fleet is dropped by the next star step.
        var visitor = game.CreateFleet(b, home, home.Planets.First(p => p.Owner == a));
        game.Step();

        Assert.DoesNotContain(visitor, b.Fleets);
        Assert.Equal(Star.Remembered, home.Sight[b.Index]);
        Assert.Equal(Star.InView, home.Sight[a.Index]);
    }

    [Fact]
    public void SightFollowsWhereEachPlayerIs()
    {
        var game = AiGames.New(77, 4, 3);
        AiGames.Run(game, 20000);

        foreach (var star in game.Universe.Stars)
        {
            foreach (var p in game.Players)
            {
                var here = !p.Eliminated && (star.Planets.Any(x => x.Owner == p) || star.Fleets.Any(f => f.Owner == p));
                if (here)
                {
                    Assert.Equal(Star.InView, star.Sight[p.Index]);
                }
            }
        }
    }
}
