using OpenPax.Sim.Players;
using OpenPax.Sim.Setup;

namespace OpenPax.Sim.Tests;

public class DiplomacyTests
{
    private static Game NewGame(int players = 3)
    {
        var tables = GameTablesForTests.Load();
        return Setup.NewGame.Create(new GameSetup(1234, 30, players, 3, [new HumanPlayerSetup("Commander", 0, 0, tables.Species[0])]), tables);
    }

    [Fact]
    public void AnOfferIsPendingOnBothSidesUntilAnswered()
    {
        var game = NewGame();
        var (a, b) = (game.Players[0], game.Players[1]);

        var events = game.OfferTreaty(b, a, Treaty.TradeRoutes) | game.TakeEvents(a);
        Assert.Equal(Treaty.OfferedByMe, b.Treaties[a.Index].Slot(Treaty.TradeRoutes));
        Assert.Equal(Treaty.OfferedToMe, a.Treaties[b.Index].Slot(Treaty.TradeRoutes));
        Assert.True(events.HasFlag(StepEvents.TreatyOffered));

        // Pending is contact, but not a level.
        Assert.False(a.Treaties[b.Index].Has(Treaty.None));
        Assert.Equal(Treaty.None, a.Treaties[b.Index].Level());

        game.ReplyTreaty(a, b, Treaty.TradeRoutes, Treaty.InForce);
        Assert.Equal(Treaty.TradeRoutes, a.Treaties[b.Index].Level());
        Assert.Equal(Treaty.TradeRoutes, b.Treaties[a.Index].Level());
    }

    [Fact]
    public void NeutralEndsAWarAndWarEndsEverything()
    {
        var game = NewGame();
        var (a, b) = (game.Players[0], game.Players[1]);
        game.ReplyTreaty(a, b, Treaty.War, Treaty.InForce);
        Assert.Equal(Treaty.War, a.Treaties[b.Index].Level());

        game.ReplyTreaty(a, b, Treaty.Neutral, Treaty.InForce);
        Assert.Equal(Treaty.Neutral, a.Treaties[b.Index].Level());
        Assert.False(a.Treaties[b.Index].Has(Treaty.War));

        game.ReplyTreaty(a, b, Treaty.JointResearch, Treaty.InForce);
        game.ReplyTreaty(a, b, Treaty.War, Treaty.InForce);
        Assert.False(a.Treaties[b.Index].Has(Treaty.JointResearch));
    }

    [Fact]
    public void PlayersWhoHaveMetDriftByTheirDisposition()
    {
        var game = NewGame();
        var (a, b) = (game.Players[0], game.Players[1]);
        game.ReplyTreaty(a, b, Treaty.Neutral, Treaty.InForce);
        var before = b.RelationScores[a.Index];
        var unmet = game.Players[2].RelationScores[a.Index];

        for (var i = 0; i < 200; i++)
        {
            game.Step();
        }

        Assert.True(b.RelationScores[a.Index] - before >= b.Relations[a.Index]);
        Assert.Equal(unmet, game.Players[2].RelationScores[a.Index]);
    }

    [Fact]
    public void AttacksUpsetTheVictimsFriendsAndPleaseItsEnemies()
    {
        var game = NewGame();
        var (attacker, victim, third) = (game.Players[0], game.Players[1], game.Players[2]);
        var before = third.RelationScores[attacker.Index];
        var victimBefore = victim.RelationScores[attacker.Index];

        Game.Incident(attacker, victim, IncidentKind.PlanetAttacked);
        Assert.Equal(Math.Max(victimBefore - 40.0, -250.0), victim.RelationScores[attacker.Index]);

        // At war with the victim: the third player warms to the attacker.
        victim.Treaties[third.Index].Write(Treaty.War, Treaty.InForce);
        Game.Incident(attacker, third, IncidentKind.EnemyAttacked);
        Assert.Equal(before + 20.0, third.RelationScores[attacker.Index]);
    }
}
