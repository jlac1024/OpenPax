using OpenPax.Sim.Players;
using OpenPax.Sim.Setup;

namespace OpenPax.Sim.Tests;

/// <summary>
/// Designs and ships from a new game against what the design builder's
/// rules give with GOG data, worked out by hand from the study of the
/// original (docs/sim/ships.md).
/// </summary>
public class DesignTests
{
    private static Game NewGame(int players, int speciesIndex = 0)
    {
        var tables = GameTablesForTests.Load();
        var setup = new GameSetup(1234, 30, players, 3, [new HumanPlayerSetup("Commander", 0, 0, tables.Species[speciesIndex])]);
        return Setup.NewGame.Create(setup, tables);
    }

    private static string Describe(Design d)
    {
        return string.Join(" ", d.Components.Select(c => c.Mode == Component.NotWeapon ? $"{c.Tech.Id}" : $"{c.Tech.Id}:{c.Mode}x{c.Count}"));
    }

    [Fact]
    public void HumanStartingDesigns()
    {
        var human = NewGame(1).Players[0];
        Assert.Equal("38 52 71 185 143 108:0x2", Describe(human.Designs[0]!));
        Assert.Equal("38 52 71 185 143 132:0x1 98:3x1 108:1x1 108:2x1 108:0x3 98:0x1", Describe(human.Designs[1]!));
        Assert.Equal("38 52 185 4", Describe(human.Designs[2]!));
        Assert.Equal([470, 990, 1200], human.Designs.Take(3).Select(d => d!.UsedSpace));
        Assert.Equal([200, 800, 100], human.Designs.Take(3).Select(d => d!.MaxStructure));
    }

    [Fact]
    public void ComputerDesignsAfterTheirStartingTechs()
    {
        var ai = NewGame(2).Players[1];

        // Techs 2 and 3 bring the research total to 125000, level 50: free
        // techs then take 82.5 percent of their space. No armor, and no
        // star drive fits types 3 to 5 yet.
        Assert.Equal(125000, ai.ResearchTotal);
        Assert.Equal("38 52 71 143 108:0x3", Describe(ai.Designs[0]!));
        Assert.Equal("38 52 71 143 132:0x1 98:3x2 108:1x1 108:2x1 108:0x5", Describe(ai.Designs[1]!));
        Assert.Equal("38 52 4", Describe(ai.Designs[2]!));
        Assert.Equal("52 71 143 132:0x2 98:3x5 108:1x3 108:2x3 108:0x11", Describe(ai.Designs[3]!));
        Assert.Equal("52 71 143 132:0x4 98:3x11 108:1x6 108:2x6 108:0x23 98:0x1", Describe(ai.Designs[4]!));
        Assert.Equal("52 71 143 132:0x1 98:3x57", Describe(ai.Designs[5]!));
        Assert.Equal([467, 976, 989], ai.Designs.Take(3).Select(d => d!.UsedSpace));
    }

    [Fact]
    public void StartingFleetsMoveAtTheTacticalDriveSpeed()
    {
        var game = NewGame(2);
        foreach (var fleet in game.Players.SelectMany(p => p.Fleets).Where(f => f.Ships.Count > 0))
        {
            Assert.Equal(2.0, fleet.Speed);
            Assert.Equal(2, fleet.JumpRange);
            Assert.Equal(2, fleet.JumpsLeft);
        }
    }

    [Fact]
    public void AquaticSpeciesAreFaster()
    {
        var tables = GameTablesForTests.Load();
        var gorak = tables.Species.ToList().FindIndex(s => s.Has(Species.Aquatic));
        var human = NewGame(1, gorak).Players[0];
        Assert.Equal(2.4, human.Fleets.First(f => f.Ships.Count > 0).Speed);
    }

    [Fact]
    public void ShipsAreCopiesWithTheirOwnIds()
    {
        var human = NewGame(1).Players[0];
        var ships = human.Fleets.SelectMany(f => f.Ships).ToList();
        Assert.Equal(ships.Count, ships.Select(s => s.ShipId).Distinct().Count());
        Assert.All(ships, s => Assert.Equal(human.Designs[s.Slot]!.Id, s.Id));
        Assert.All(ships, s => Assert.Equal(Describe(human.Designs[s.Slot]!), Describe(s)));
    }
}
