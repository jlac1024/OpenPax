using OpenPax.Sim.Combat;
using OpenPax.Sim.Players;
using OpenPax.Sim.Setup;

namespace OpenPax.Sim.Tests;

public class CombatThinkingTests
{
    /// <summary>A human fleet against a computer player's fleet, at war or not.</summary>
    private static (Game Game, Battle Battle, List<BattleShip> Human, List<BattleShip> Computer) NewBattle(bool war = true, int extraShips = 3)
    {
        var tables = GameTablesForTests.Load();
        var game = NewGame.Create(new GameSetup(1234, 30, 3, 3, [new HumanPlayerSetup("Commander", 0, 0, tables.Species[0])]), tables);
        var human = game.Players[0];
        var computer = game.Players[1];
        var ours = human.Fleets.First(f => f.Ships.Count > 0);
        var theirs = computer.Fleets.First(f => f.Ships.Count > 0);
        var armed = human.Designs.OfType<Design>().OrderByDescending(d => d.Weapons.Count).First();
        for (var i = 0; i < extraShips; i++)
        {
            ours.AddShip(game.NewShip(armed), tables.Tech);
        }

        if (war)
        {
            var events = StepEvents.None;
            game.SetTreaty(human, computer, Treaty.War, Treaty.InForce, ref events);
            game.SetTreaty(computer, human, Treaty.War, Treaty.InForce, ref events);
        }

        game.StartFleetBattle(ours, theirs);
        game.FightStepByStep = true;
        var battle = game.Battles[0];
        return (game, battle, battle.Ships[human.Index], battle.Ships[computer.Index]);
    }

    private static void Steps(Game game, Battle battle, int n)
    {
        for (var i = 0; i < n && game.Battles.Contains(battle); i++)
        {
            game.Step();
        }
    }

    [Fact]
    public void ASideNotesItsFirstShipWhenTheBattleBegins()
    {
        var (game, battle, human, computer) = NewBattle();
        game.Step();
        var c = battle.Commanders[computer[0].Owner]!; // the computer side has a commander
        Assert.Equal(computer[^1].Ship.Strength(game.Tables.Tech), c.StartStrength);
        Assert.Equal(c.StartStrength, c.Strength);
        Assert.Equal(computer.Sum(s => s.Ship.Structure), c.StartStructure);
        Assert.Equal(0, c.Kills);
        Assert.NotEqual(0, battle.Commanders[human[0].Owner]!.StartStrength); // and so has the human side
    }

    [Fact]
    public void AComputerSideWaitsSevenThinksThenAttacks()
    {
        var (game, battle, _, computer) = NewBattle();
        var c = battle.Commanders[computer[0].Owner]!; // the computer side has a commander
        var p = c.Player;

        // It thinks once every 20 steps, in its own sub-step.
        Steps(game, battle, 20 * 7);
        Assert.Equal(7, c.Counter);
        Assert.All(computer, s => Assert.Equal(BattleShip.Idle, s.State));
        Steps(game, battle, 20);
        Assert.Equal(8, c.Counter);
        Assert.Contains(computer, s => s.State is BattleShip.Pursuing or BattleShip.Attacking && s.Target is not null);
        Assert.Equal(p, c.Player);
    }

    [Fact]
    public void AComputerSideWithNoEnemyGivesUpAndRetreats()
    {
        var (game, battle, _, computer) = NewBattle(war: false);
        var c = battle.Commanders[computer[0].Owner]!; // the computer side has a commander
        Steps(game, battle, 20 * 8);
        Assert.True(c.Done);
        Assert.All(computer, s => Assert.Equal(BattleShip.Leaving, s.State));
    }

    [Fact]
    public void AutoMakesTheHumanSideFightAtOnceAndNeverRetreat()
    {
        var (game, battle, human, _) = NewBattle();
        var c = battle.Commanders[human[0].Owner]!; // the human side has a commander
        game.ApplyBattleOrder(new BattleOrder(BattleOrderKind.Horizontal, battle.Id, human[0].Id, -1, 0, 0, []));
        Assert.True(c.Computer);
        Assert.True(battle.AutoEngaged);
        Assert.Equal(8, c.Counter);
        Assert.All(human, s => Assert.Equal(BattleShip.Pursuing, s.State));
        Steps(game, battle, 400);
        Assert.False(c.Done);
        Assert.DoesNotContain(human, s => s.State == BattleShip.Leaving);

        game.ApplyBattleOrder(new BattleOrder(BattleOrderKind.Horizontal, battle.Id, human[0].Id, -1, 0, 0, []));
        Assert.False(c.Computer);
        Assert.False(battle.AutoEngaged);
        Assert.All(human, s => Assert.Equal(BattleShip.Idle, s.State));
    }

    [Fact]
    public void AWellBalancedSideNeverRollsToRetreat()
    {
        var (game, battle, _, computer) = NewBattle();
        var c = battle.Commanders[computer[0].Owner]!; // the computer side has a commander
        game.Step();
        var t = (int)typeof(Game).GetMethod("RetreatThreshold", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)!
            .Invoke(game, [battle, c])!; // the method exists and returns an int
        Assert.True(t >= 140);
    }

    [Fact]
    public void TheSameBattleThinksTheSameWay()
    {
        static string Run()
        {
            var (game, battle, human, computer) = NewBattle();
            game.ApplyBattleOrder(new BattleOrder(BattleOrderKind.Attack, battle.Id, computer[0].Id, -1, 0, 0, [.. human.Select(s => s.Id)]));
            Steps(game, battle, 1500);
            return game.Random.State + "|" + string.Join(";", human.Concat(computer).Select(s => $"{s.Id}:{s.PosX}:{s.PosY}:{s.State}:{s.Ship.Structure}:{s.Dead}"));
        }

        Assert.Equal(Run(), Run());
    }
}
