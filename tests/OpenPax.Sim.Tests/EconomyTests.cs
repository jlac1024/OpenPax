using OpenPax.Sim.Galaxy;
using OpenPax.Sim.Players;
using OpenPax.Sim.Setup;

namespace OpenPax.Sim.Tests;

public class EconomyTests
{
    private static Game NewGame(int players = 2, int difficulty = 3)
    {
        var tables = GameTablesForTests.Load();
        var setup = new GameSetup(1234, 30, players, difficulty, [new HumanPlayerSetup("Commander", 0, 0, tables.Species[0])]);
        return Setup.NewGame.Create(setup, tables);
    }

    [Fact]
    public void EveryPlanHoldsEachImprovementOnce()
    {
        var game = NewGame();
        var improvements = game.Tables.OfKind(TechInfo.ImprovementKind).Select(t => t.Id).Order().ToList();
        Assert.Equal(50, improvements.Count);
        // The six stock plans; slots 6 to 9 start empty.
        var plans = game.Players[0].Plans;
        Assert.Equal(6, plans.Count(p => p is not null));
        foreach (var plan in plans.OfType<int[]>())
        {
            Assert.Equal(improvements, plan.Order());
        }

        // Plan 1 puts population improvements first, then by build cost.
        var growth = (plans[1] ?? []).Select(game.Tables.Tech).ToList();
        var firstOther = growth.FindIndex(t => t.Resource != Colony.Population);
        Assert.True(firstOther > 0);
        Assert.All(growth.Skip(firstOther), t => Assert.NotEqual(Colony.Population, t.Resource));
    }

    [Fact]
    public void TheHomeworldStartsWithEveryKnownImprovementBuilt()
    {
        var game = NewGame();
        foreach (var p in game.Players)
        {
            var colony = p.Colonies.Single();
            foreach (var tech in game.Tables.OfKind(TechInfo.ImprovementKind))
            {
                var slot = colony.Improvements[tech.Id - game.Tables.OfKind(TechInfo.ImprovementKind).First().Id];
                Assert.Equal(p.KnownTechs.Contains(tech.Id), slot is not null);
                Assert.True(slot is null || slot.Complete);
            }

            Assert.Equal(colony.MaxPopulation, colony.CurrentPopulation);
            Assert.Equal(100.0, p.Support);
        }
    }

    [Fact]
    public void TheFirstEconomyPassFollowsTheFormulas()
    {
        var game = NewGame();
        var p = game.Players[0];
        var c = p.Colonies.Single();
        var money = p.Money;
        var rate = 1.0 - p.Species.RateModifiers[4] * -0.1;
        var credits = (c.Flat[0] + c.CurrentPopulation * c.PerPopulation[0]) * c.Planet.Score * rate * 0.1;
        var upkeep = (((p.StructureUpkeep[2] + p.StructureUpkeep[1]) + p.StructureUpkeep[0]) + p.Upkeep) * 0.1;

        for (var i = 0; i < 40; i++)
        {
            game.Step();
        }

        Assert.Equal(credits, c.Resources[Colony.Credits]);
        Assert.Equal(credits, p.Income);
        Assert.True(p.ResearchPoints > 0.0);

        // Nothing is funded and every improvement is paid at full spending.
        var charges = p.ImprovementUpkeep[1] * 100.0 * 0.001 + p.ImprovementUpkeep[2] * 100.0 * 0.001 + p.ImprovementUpkeep[3] * 100.0 * 0.001;
        Assert.Equal(money + credits - upkeep - charges, p.Money, 9);
    }

    [Fact]
    public void AColonyShipFoundsAColony()
    {
        // A seed whose home system has a free planet that is not a gas giant.
        var tables = GameTablesForTests.Load();
        Game game;
        Planet? target;
        var seed = 1u;
        do
        {
            var setup = new GameSetup(seed++, 30, 2, 3, [new HumanPlayerSetup("Commander", 0, 0, tables.Species[0])]);
            game = Setup.NewGame.Create(setup, tables);
            target = game.Universe.Stars[game.Players[0].HomeStar].Planets.FirstOrDefault(p => p.Colony is null && p.Class != 5);
        }
        while (target is null);

        var human = game.Players[0];
        var fleet = human.Fleets.First(f => f.Ships.Count > 0 && f.Ships[0].Type == 2);
        var ships = fleet.Ships.Count;

        game.GiveOrder(fleet, Order.To(target));
        game.SetMode(fleet, FleetMode.Colonize);
        for (var i = 0; i < 2000 && target.Colony is null; i++)
        {
            game.Step();
        }

        Assert.NotNull(target.Colony);
        Assert.Same(human, target.Owner);
        Assert.Same(target.Colony, human.Colonies[0]);
        Assert.Equal(ships - 1, fleet.Ships.Count);
        Assert.Equal(FleetMode.None, fleet.Mode);

        // The home star already has its governor.
        Assert.Single(human.Advisors, a => a.Role == Advisor.GovernorRole);
    }

    [Fact]
    public void UnpaidUpkeepWearsThingsDown()
    {
        var game = NewGame();
        var p = game.Players[0];
        p.Money = 0.0;
        p.Upkeep = 1e9;
        var draws = game.Random.Draws;
        for (var i = 0; i < 40; i++)
        {
            game.Step();
        }

        Assert.True(p.Support < 100.0);
        Assert.True(game.Random.Draws > draws);
    }

    [Fact]
    public void LongGamesReplayIdentically()
    {
        ulong Run()
        {
            var game = NewGame(4);
            for (var i = 0; i < 4000; i++)
            {
                game.Step();
            }

            return game.Hash();
        }

        Assert.Equal(Run(), Run());
    }
}
