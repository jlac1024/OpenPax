using OpenPax.Sim.Galaxy;
using OpenPax.Sim.Setup;

namespace OpenPax.Sim.Tests;

public class CheatTests
{
    private static Game NewGameForCheats()
    {
        var tables = GameTablesForTests.Load();
        return NewGame.Create(new GameSetup(1234, 30, 3, 3, [new HumanPlayerSetup("Commander", 0, 0, tables.Species[0])]), tables);
    }

    [Fact]
    public void MoneyResearchAndExploreDoWhatTheySay()
    {
        var game = NewGameForCheats();
        var human = game.Players[0];
        game.ApplyCheat(human, Cheat.GiveLotsOfMoney);
        Assert.Equal(900000000.0, human.Money);
        game.ApplyCheat(human, Cheat.ResearchAll);
        Assert.All(game.Tables.Techs, t => Assert.Contains(t.Id, human.KnownTechs));
        game.ApplyCheat(human, Cheat.ExploreAll);
        Assert.All(game.Universe.Stars, s => Assert.NotEqual(Star.Unseen, s.Sight[human.Index]));
    }

    [Fact]
    public void ResearchAllOpensEveryHull()
    {
        var game = NewGameForCheats();
        var human = game.Players[0];
        game.ApplyCheat(human, Cheat.ResearchAll);
        var best = game.Tables.OfKind(Players.TechInfo.ArmorKind).Last();
        Assert.Equal(best.Id, human.BestArmor);
        Assert.Equal(5, best.ArmorHullLimit);
    }

    [Fact]
    public void ShipsAndColoniesAppear()
    {
        var game = NewGameForCheats();
        var human = game.Players[0];
        var fleets = human.Fleets.Count;
        game.ApplyCheat(human, Cheat.MoreShips);
        Assert.Equal(fleets + 1, human.Fleets.Count);
        Assert.NotEmpty(human.Fleets[0].Ships);

        var colonies = human.Colonies.Count;
        game.ApplyCheat(human, Cheat.ColonizeSectors);
        Assert.True(human.Colonies.Count > colonies);
    }

    [Fact]
    public void BuildAllFillsTheColonies()
    {
        var game = NewGameForCheats();
        var human = game.Players[0];
        game.ApplyCheat(human, Cheat.ResearchAll);
        game.ApplyCheat(human, Cheat.BuildAll);
        var home = human.Colonies[0];
        Assert.NotNull(home.FirstOfKind(Players.TechInfo.ShipyardKind));
    }

    [Fact]
    public void TheCombatCheatsSendAFleetAtAnotherPlayer()
    {
        var game = NewGameForCheats();
        var human = game.Players[0];
        game.ApplyCheat(human, Cheat.FleetCombat);
        Assert.NotEmpty(human.Fleets[0].Orders);
        game.ApplyCheat(human, Cheat.PlanetCombat);
        Assert.NotEmpty(human.Fleets[0].Orders);
    }

    [Fact]
    public void KillPlayerEliminatesThem()
    {
        var game = NewGameForCheats();
        var computer = game.Players[2];
        game.ApplyCheat(computer, Cheat.KillPlayer);
        Assert.True(computer.Eliminated);
    }
}
