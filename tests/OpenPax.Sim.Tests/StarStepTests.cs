using OpenPax.Sim.Galaxy;
using OpenPax.Sim.Setup;

namespace OpenPax.Sim.Tests;

public class StarStepTests
{
    private static Game NewGame(int players, int difficulty)
    {
        var tables = GameTablesForTests.Load();
        var setup = new GameSetup(1234, 30, players, difficulty, [new HumanPlayerSetup("Commander", 0, 0, tables.Species[0])]);
        return Setup.NewGame.Create(setup, tables);
    }

    [Fact]
    public void EveryPlanetTakesOneOrbitStepPerTick()
    {
        var game = NewGame(4, 3);
        var expected = game.Universe.Stars.SelectMany(s => s.Planets)
            .Select(p => new Orbit(p.Orbit.CenterX, p.Orbit.CenterY, p.Orbit.Radius, p.Orbit.Angle, p.Orbit.Speed))
            .ToList();
        for (var tick = 0; tick < 3; tick++)
        {
            game.Step();
            foreach (var o in expected)
            {
                o.Step();
            }
        }

        var planets = game.Universe.Stars.SelectMany(s => s.Planets).ToList();
        Assert.Equal(expected.Select(o => (o.Angle, o.X, o.Y)), planets.Select(p => (p.Orbit.Angle, p.Orbit.X, p.Orbit.Y)));
    }

    [Fact]
    public void TicksDrawNothingYet()
    {
        var game = NewGame(4, 3);
        var draws = game.Random.Draws;
        for (var i = 0; i < 1000; i++)
        {
            game.Step();
        }

        Assert.Equal(draws, game.Random.Draws);
    }

    [Fact]
    public void SystemsHoldTheSameFleetsAsTheirOwners()
    {
        var game = NewGame(4, 2);
        var inSystems = game.Universe.Stars.SelectMany(s => s.Fleets).Select(f => f.ObjectId).Order();
        var owned = game.Players.SelectMany(p => p.Fleets).Select(f => f.ObjectId).Order();
        Assert.Equal(owned, inSystems);
    }

    [Fact]
    public void TheEmptyStartingFleetIsRemovedOnTheFirstTick()
    {
        var game = NewGame(4, 5);
        var human = game.Players[0];
        Assert.Contains(human.Fleets, f => f.Ships.Count == 0);

        var events = game.Step() | game.TakeEvents(human);

        Assert.DoesNotContain(human.Fleets, f => f.Ships.Count == 0);
        Assert.DoesNotContain(game.Universe.Stars[human.HomeStar].Fleets, f => f.Ships.Count == 0);
        Assert.Equal(StepEvents.None, events);
        Assert.False(game.Over);
    }

    [Fact]
    public void ALoneHumanAtDifficultyFiveWinsOnTheFirstTick()
    {
        // The original's quirk: removing the empty fleet runs the victory
        // check, and with nobody else in the game nothing keeps it open.
        var game = NewGame(1, 5);
        var human = game.Players[0];
        Assert.Equal(StepEvents.Victory, game.Step() | game.TakeEvents(human));
        Assert.True(game.Over);
        Assert.Equal(StepEvents.None, game.Step() | game.TakeEvents(human));
    }

    [Fact]
    public void ALoneHumanAtEasierLevelsPlaysOn()
    {
        var game = NewGame(1, 4);
        for (var i = 0; i < 100; i++)
        {
            Assert.Equal(StepEvents.None, game.Step() | game.TakeEvents(game.Players[0]));
        }

        Assert.False(game.Over);
    }
}
