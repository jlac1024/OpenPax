using OpenPax.Sim.Numerics;
using OpenPax.Sim.Players;
using OpenPax.Sim.Setup;

namespace OpenPax.Sim.Tests;

public class ResearchTests
{
    private static Game NewGame(int players = 2, int difficulty = 3)
    {
        var tables = GameTablesForTests.Load();
        var setup = new GameSetup(1234, 30, players, difficulty, [new HumanPlayerSetup("Commander", 0, 0, tables.Species[0])]);
        return Setup.NewGame.Create(setup, tables);
    }

    [Fact]
    public void EveryFieldStartsOnItsCheapestTechForAHuman()
    {
        var game = NewGame();
        var human = game.Players[0];
        for (var field = 0; field < 5; field++)
        {
            var slot = human.ResearchSlots[field];
            var open = game.Tables.Techs
                .Where(t => t.ResearchField == field && !human.Knows(t.Id) && t.Prerequisites.All(human.Knows))
                .ToList();
            if (open.Count == 0)
            {
                Assert.Equal(ResearchSlot.None, slot.Tech);
                continue;
            }

            var cheapest = open.MinBy(t => TechLevels.ResearchCost(t, human))!;
            Assert.Equal(cheapest.Id, slot.Tech);
            Assert.Equal(TechLevels.ResearchCost(cheapest, human), slot.Cost);
            Assert.Equal(0, slot.Progress);
        }
    }

    [Fact]
    public void AFieldGetsItsShareOfTheResearchPoints()
    {
        var game = NewGame();
        var human = game.Players[0];
        for (var i = 0; i < 40; i++)
        {
            game.Step();
        }

        // The economy pass makes the points in the same tick, just before research.
        var slot = human.ResearchSlots[0];
        var points = human.Research[0] * (human.ResearchPoints * 10.0) * 0.01 * 0.1;
        if (slot.Tech != ResearchSlot.None && slot.Progress < slot.Cost)
        {
            Assert.Equal(X87.ToInt(points), slot.Progress);
            Assert.Equal(points - X87.ToInt(points), slot.Fraction);
        }
    }

    [Fact]
    public void TechsAreLearnedAndResearchMovesOn()
    {
        var game = NewGame(4);

        // Spread evenly, a first tech takes some 300 to 400 periods (the
        // original's pace, before computer players manage their research);
        // all on weapons it takes about 80.
        foreach (var p in game.Players)
        {
            p.Research[0] = 100;
            for (var field = 1; field < 5; field++)
            {
                p.Research[field] = 0;
            }
        }

        var known = game.Players.Select(p => p.KnownTechs.Count).ToList();
        var aiDesign = game.Players[1].Designs[0]!.Id;
        for (var i = 0; i < 8000; i++)
        {
            game.Step();
        }

        for (var p = 0; p < game.Players.Count; p++)
        {
            Assert.True(game.Players[p].KnownTechs.Count > known[p], $"player {p} learned nothing");
            Assert.Contains(game.Players[p].ResearchSlots, s => s.Tech != ResearchSlot.None);
        }

        // A computer player redesigns on every new tech, so its designs get new ids.
        Assert.NotEqual(aiDesign, game.Players[1].Designs[0]!.Id);
    }

    [Fact]
    public void ResearchKeepsGamesDeterministic()
    {
        ulong Run()
        {
            var game = NewGame(4, 5);
            for (var i = 0; i < 6000; i++)
            {
                game.Step();
            }

            return game.Hash();
        }

        Assert.Equal(Run(), Run());
    }
}
