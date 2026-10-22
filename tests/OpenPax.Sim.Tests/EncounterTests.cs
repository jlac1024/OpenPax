using OpenPax.Sim.Galaxy;
using OpenPax.Sim.Players;
using OpenPax.Sim.Setup;

namespace OpenPax.Sim.Tests;

public class EncounterTests
{
    /// <summary>The human's first fleet set to intercept, and a computer fleet brought into the same system.</summary>
    private static (Game Game, Star Star, Fleet Ours, Fleet Theirs) Meeting(int treaty)
    {
        var tables = GameTablesForTests.Load();
        var game = NewGame.Create(new GameSetup(1234, 30, 3, 3, [new HumanPlayerSetup("Commander", 0, 0, tables.Species[0])]), tables);
        var human = game.Players[0];
        var computer = game.Players[1];
        var ours = human.Fleets.First(f => f.Ships.Count > 0);
        var theirs = computer.Fleets.First(f => f.Ships.Count > 0);
        var star = game.Universe.Stars[ours.Star];
        game.Universe.Stars[theirs.Star].Fleets.Remove(theirs);
        theirs.Star = ours.Star;
        star.Fleets.Insert(0, theirs);
        ours.Intercepting = true;
        var events = StepEvents.None;
        game.SetTreaty(human, computer, treaty, Treaty.InForce, ref events);
        return (game, star, ours, theirs);
    }

    [Fact]
    public void AnInterceptorGoesAfterAFleetAtWar()
    {
        var (game, star, ours, theirs) = Meeting(Treaty.War);
        var draws = game.Random.State;
        game.Encounter(star, ours);
        Assert.Same(theirs, Assert.Single(ours.Orders).Target);
        Assert.NotEqual(draws, game.Random.State);
    }

    [Fact]
    public void ANeutralFleetIsHostileOnlyWhenMakingForTheOwnersColony()
    {
        var (game, star, ours, theirs) = Meeting(Treaty.Neutral);
        game.Encounter(star, ours);
        Assert.Empty(ours.Orders);

        game.GiveOrder(theirs, Order.To(ours.Owner.Colonies[0].Planet));
        game.Encounter(star, ours);
        Assert.Same(theirs, Assert.Single(ours.Orders).Target);
    }

    [Fact]
    public void AFleetNotInterceptingIgnoresTheOthers()
    {
        var (game, star, ours, _) = Meeting(Treaty.War);
        ours.Intercepting = false;
        var draws = game.Random.State;
        game.Encounter(star, ours);
        Assert.Empty(ours.Orders);
        Assert.Equal(draws, game.Random.State);
    }

    [Fact]
    public void MeetingAFriendlyFleetEndsAChaseOfAnother()
    {
        var (game, star, ours, theirs) = Meeting(Treaty.Allied);
        var third = game.Players[2].Fleets.First(f => f.Ships.Count > 0);
        game.GiveOrder(ours, Order.To(third));
        game.Encounter(star, ours);
        Assert.Empty(ours.Orders);
        Assert.NotSame(theirs, third);
    }
}
