using OpenPax.Sim.Players;
using OpenPax.Sim.Setup;

namespace OpenPax.Sim.Tests;

public class LedgerTests
{
    private static (Game Game, Player Human) Played(int ticks)
    {
        var tables = GameTablesForTests.Load();
        var game = NewGame.Create(new GameSetup(1234, 30, 2, 3, [new HumanPlayerSetup("Commander", 0, 0, tables.Species[0])]), tables);
        for (var i = 0; i < ticks; i++)
        {
            game.Step();
        }

        return (game, game.Players[0]);
    }

    [Fact]
    public void TheLinesAddUpToTheNetWithinRounding()
    {
        var (_, p) = Played(400);
        var lines = Game.AnnualIncome(p) - Game.SpendingCost(p, 0) - Game.SpendingCost(p, 1) - Game.SpendingCost(p, 2)
            - Game.SubsidyCost(p) - p.Upkeep;
        Assert.Equal(lines, Game.NetIncome(p), 6);
    }

    [Fact]
    public void LowerSpendingCostsLess()
    {
        var (_, p) = Played(400);
        var before = Game.NetIncome(p);
        Game.SetSpending(p, 0, 0);
        Assert.Equal(0.0, Game.SpendingCost(p, 0));
        Assert.True(Game.NetIncome(p) >= before);
    }

    [Fact]
    public void ColonyUpkeepTakesTheSpendingPercents()
    {
        var (_, p) = Played(400);
        var c = p.Colonies[0];
        Game.SetSpending(p, 0, 0);
        Game.SetSpending(p, 1, 0);
        Game.SetSpending(p, 2, 0);
        Assert.Equal(c.StructureUpkeep, Game.ColonyUpkeep(c));
    }

    [Fact]
    public void PlansStepRoundTheStockSix()
    {
        var (_, p) = Played(0);
        var c = p.Colonies[0];
        Game.SetColonyPlan(c.Planet, 5);
        Assert.Equal(0, Game.StepPlan(c, 1));
        Game.SetColonyPlan(c.Planet, 0);
        Assert.Equal(5, Game.StepPlan(c, -1));
        Assert.Equal(1, Game.StepPlan(c, 1));
    }

    [Fact]
    public void APlanetInBattleKeepsItsPlan()
    {
        var (_, p) = Played(0);
        var c = p.Colonies[0];
        var plan = c.Plan;
        c.Planet.InCombat = true;
        Game.SetColonyPlan(c.Planet, (plan + 1) % 6);
        Assert.Equal(plan, c.Plan);
    }
}
