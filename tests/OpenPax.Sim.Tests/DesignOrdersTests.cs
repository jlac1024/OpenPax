using OpenPax.Sim.Players;
using OpenPax.Sim.Setup;

namespace OpenPax.Sim.Tests;

public class DesignOrdersTests
{
    private static (Game Game, Player Human) NewHumanGame()
    {
        var tables = GameTablesForTests.Load();
        var game = NewGame.Create(new GameSetup(1234, 30, 2, 3, [new HumanPlayerSetup("Commander", 0, 0, tables.Species[0])]), tables);
        return (game, game.Players[0]);
    }

    [Fact]
    public void ANewDesignIsABlankScoutWithTheBestArmor()
    {
        var (game, human) = NewHumanGame();
        var design = game.StartDesign(human, null);
        Assert.Equal(0, design.Type);
        Assert.Equal(0, design.Quality);
        Assert.Equal(human.BestArmor, design.Armor?.Tech.Id ?? 0);
        Assert.Null(design.TacticalDrive);
    }

    [Fact]
    public void RemoveTakesOneUnitAtATime()
    {
        var (game, human) = NewHumanGame();
        var design = game.AutoDesign(human, 1, "Test");
        var weapon = design.Weapons[0];
        var (tech, mode, count, space) = (weapon.Tech, weapon.Mode, weapon.Count, design.UsedSpace);

        design.Remove(tech, mode);
        Assert.True(design.UsedSpace < space);
        if (count == 1)
        {
            Assert.DoesNotContain(design.Weapons, w => w.Tech == tech && w.Mode == mode);
        }
        else
        {
            Assert.Equal(count - 1, design.Weapons.First(w => w.Tech == tech && w.Mode == mode).Count);
        }

        var drive = design.TacticalDrive!.Tech;
        design.Remove(drive, Component.NotWeapon);
        Assert.Null(design.TacticalDrive);
    }

    [Fact]
    public void SavingTakesTheFirstFreeSlotWithANewClassNumber()
    {
        var (game, human) = NewHumanGame();
        var design = game.AutoDesign(human, 1, "Test");
        var counter = human.DesignCounters[1];
        var free = Array.IndexOf(human.Designs, null);

        var slot = game.SaveDesign(human, design);
        Assert.Equal(free, slot);
        var saved = human.Designs[slot]!;
        Assert.NotSame(design, saved);
        Assert.Equal("Test", saved.Name);
        Assert.Equal(game.Tables.ClassNames[1] + counter, saved.ClassName);
        Assert.Equal(counter + 1, human.DesignCounters[1]);

        Game.DeleteDesign(human, slot);
        Assert.Null(human.Designs[slot]);
    }

    [Fact]
    public void FirepowerSumsAnArcsBand()
    {
        var (game, human) = NewHumanGame();
        var design = game.AutoDesign(human, 4, "Test");
        foreach (var mode in Enumerable.Range(0, 4))
        {
            var expected = design.Weapons.Where(w => w.Mode == mode && w.Tech.WeaponType != 1).Sum(w => w.Tech.Damage[0] * w.Count);
            Assert.Equal(expected, design.Firepower(mode, 0));
        }
    }
}
