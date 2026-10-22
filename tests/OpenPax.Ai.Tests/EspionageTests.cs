using OpenPax.Sim;
using OpenPax.Sim.Players;

namespace OpenPax.Ai.Tests;

public class EspionageTests
{
    [Fact]
    public void MissionCostsFollowKindAndLevel()
    {
        var game = AiGames.New(1234, 2, 3);
        var (a, b) = (game.Players[0], game.Players[1]);
        Assert.Equal(50.0, game.AddMission(a, b, SpyMission.StealShips, 0).Cost);
        Assert.Equal(50.0, game.AddMission(a, b, SpyMission.Sabotage, 1).Cost);
        Assert.Equal(120.0, game.AddMission(a, b, SpyMission.StealTechs, 2).Cost);
    }

    [Fact]
    public void PointsGoIntoMissionsInOrder()
    {
        var game = AiGames.New(1234, 2, 3);
        var (a, b) = (game.Players[0], game.Players[1]);
        var first = game.AddMission(a, b, SpyMission.LocateColony, 0);
        var second = game.AddMission(a, b, SpyMission.LocateColony, 0);
        var events = StepEvents.None;

        a.EspionagePoints = 30.0;
        game.SpendEspionage(a, ref events);
        Assert.Equal(30.0, first.Progress);
        Assert.Equal(0.0, second.Progress);
        Assert.Equal(60, first.Percent);

        // 20 finish the first, which is tried and goes; the other 10 carry on.
        a.EspionagePoints = 30.0;
        game.SpendEspionage(a, ref events);
        Assert.Equal([second], a.Missions);
        Assert.Equal(10.0, second.Progress);
    }

    [Fact]
    public void SpiesStealTechsInTheEnd()
    {
        var game = AiGames.New(1234, 2, 3);
        var (a, b) = (game.Players[0], game.Players[1]);
        var stolen = game.Tables.Techs.First(t => !a.Knows(t.Id) && !b.Knows(t.Id));
        game.GiveTech(b, stolen);
        b.EspionagePoints = 0.0;

        var events = StepEvents.None;
        for (var i = 0; i < 500 && !a.Knows(stolen.Id); i++)
        {
            var m = game.AddMission(a, b, SpyMission.StealTechs, 2);
            a.EspionagePoints = m.Cost;
            game.SpendEspionage(a, ref events);
        }

        Assert.True(a.Knows(stolen.Id));
        Assert.Empty(a.Missions);
    }

    [Fact]
    public void SpiesTurnAdvisorsAgainstTheirOwners()
    {
        var game = AiGames.New(1234, 2, 3);
        var (a, b) = (game.Players[0], game.Players[1]);
        b.EspionagePoints = 0.0;

        var events = StepEvents.None;
        for (var i = 0; i < 500 && !b.Advisors.Any(x => x.TurnedBy == a); i++)
        {
            var m = game.AddMission(a, b, SpyMission.TurnAdvisors, 2);
            a.EspionagePoints = m.Cost;
            game.SpendEspionage(a, ref events);
        }

        var turned = b.Advisors.First(x => x.TurnedBy == a);
        Assert.False(turned.IsGovernor);
        Assert.True(turned.Value(turned.Role) <= 0.0);
    }

    [Fact]
    public void ComputerPlayersSpyOnPlayersTheyHaveMet()
    {
        var game = AiGames.New(77, 4, 3);
        AiGames.Run(game, 20000);

        Assert.True(game.NextMissionId > 0);
        Assert.All(game.Players.SelectMany(p => p.Missions), m =>
        {
            Assert.NotEqual(m.Owner, m.Target);
            Assert.NotEqual(SpyMission.RevealSystems, m.Kind);
            Assert.NotEqual(Treaty.None, m.Owner.Treaties[m.Target.Index].Level());
        });
    }
}
