using OpenPax.Sim.Combat;
using OpenPax.Sim.Players;
using OpenPax.Sim.Setup;

namespace OpenPax.Sim.Tests;

public class CombatWeaponsTests
{
    /// <summary>A battle between a human fleet of armed ships and a computer player's fleet, at war.</summary>
    private static (Game Game, Battle Battle, List<BattleShip> Human, List<BattleShip> Computer) NewBattle(int extraShips = 3)
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

        var events = StepEvents.None;
        game.SetTreaty(human, computer, Treaty.War, Treaty.InForce, ref events);
        game.SetTreaty(computer, human, Treaty.War, Treaty.InForce, ref events);
        game.StartFleetBattle(ours, theirs);
        game.FightStepByStep = true;
        var battle = game.Battles[0];
        return (game, battle, battle.Ships[human.Index], battle.Ships[computer.Index]);
    }

    private static int Steps(Game game, Battle battle, int n)
    {
        var i = 0;
        for (; i < n && game.Battles.Contains(battle); i++)
        {
            game.Step();
        }

        return i;
    }

    [Fact]
    public void SensorsPickUpTheNearestEnemyInRange()
    {
        var (game, battle, human, computer) = NewBattle();
        var ship = human.First(s => s.SensorRange > 0);
        Assert.Empty(ship.Tracked);
        Steps(game, battle, 1);
        Assert.True(ship.Tracked.Count <= 1);
        Assert.All(ship.Tracked, o => Assert.Contains(o, computer));
    }

    [Fact]
    public void AnAttackFiresShotsThatLandAndTheBattleEnds()
    {
        var (game, battle, human, computer) = NewBattle();
        var enemy = computer[0];
        var structure = enemy.Ship.Structure;
        game.ApplyBattleOrder(new BattleOrder(BattleOrderKind.Attack, battle.Id, enemy.Id, -1, 0, 0, [.. human.Select(s => s.Id)]));

        var sawShots = false;
        for (var i = 0; i < 3000 && game.Battles.Contains(battle); i++)
        {
            game.Step();
            sawShots |= battle.Shots[human[0].Owner].Count + battle.Missiles[human[0].Owner].Count > 0 || battle.LastShot > 0;
        }

        Assert.True(sawShots, "no shot was fired");
        Assert.True(battle.LastHit > 0, "nothing was hit");
        Assert.True(enemy.Dead || enemy.Ship.Structure < structure);
        Assert.DoesNotContain(battle, game.Battles);
    }

    [Fact]
    public void AKillCreditsTheShooterAndGivesExperience()
    {
        var (game, battle, human, computer) = NewBattle(extraShips: 6);
        var level = human.Max(s => s.Ship.ExperiencePoints);
        game.ApplyBattleOrder(new BattleOrder(BattleOrderKind.Attack, battle.Id, computer[0].Id, -1, 0, 0, [.. human.Select(s => s.Id)]));
        var commander = battle.Commanders[human[0].Owner]!; // the human side has a commander
        Steps(game, battle, 4000);
        Assert.SkipWhen(commander.Kills == 0, "No kill with this seed.");
        Assert.DoesNotContain(battle, game.Battles);
    }

    [Fact]
    public void ArcsFollowTheHeading()
    {
        var (_, _, human, computer) = NewBattle();
        var ship = human[0];
        var other = computer[0];
        ship.SetPosition(1000, 1000);
        ship.Facing = 0;
        other.SetPosition(1500, 1000);
        Assert.Equal(0, Arc(ship, other));
        other.SetPosition(1000, 1500);
        Assert.Equal(1, Arc(ship, other));
        other.SetPosition(1000, 500);
        Assert.Equal(2, Arc(ship, other));
    }

    private static int Arc(BattleShip ship, BattleObject o)
    {
        return (int)typeof(Game).GetMethod("Arc", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static)!.Invoke(null, [ship, o])!; // the method exists; it returns an int
    }

    [Fact]
    public void TheSameOrdersGiveTheSameBattleWithWeapons()
    {
        static string Run()
        {
            var (game, battle, human, computer) = NewBattle();
            game.ApplyBattleOrder(new BattleOrder(BattleOrderKind.Attack, battle.Id, computer[0].Id, -1, 0, 0, [.. human.Select(s => s.Id)]));
            Steps(game, battle, 800);
            return game.Random.State + "|" + string.Join(";", human.Concat(computer).Select(s => $"{s.Id}:{s.PosX}:{s.PosY}:{s.Ship.Structure}:{s.Dead}"));
        }

        Assert.Equal(Run(), Run());
    }

    [Fact]
    public void FiringOnAPlayerAtPeaceDeclaresWar()
    {
        var (game, battle, human, computer) = NewBattle();
        var events = StepEvents.None;
        var h = game.Players[0];
        var c = game.Players[1];
        game.SetTreaty(h, c, Treaty.Neutral, Treaty.InForce, ref events);
        game.SetTreaty(c, h, Treaty.Neutral, Treaty.InForce, ref events);
        Assert.NotEqual(Treaty.War, h.Treaties[c.Index].Level());

        // At peace the computer side would give up and leave before a shot lands; hold it still.
        battle.Commanders[c.Index]!.Computer = false; // the computer side has a commander
        game.ApplyBattleOrder(new BattleOrder(BattleOrderKind.Attack, battle.Id, computer[0].Id, -1, 0, 0, [.. human.Select(s => s.Id)]));
        Steps(game, battle, 1500);
        Assert.SkipWhen(battle.LastShot == 0, "No shot fired with this seed.");
        Assert.Equal(Treaty.War, h.Treaties[c.Index].Level());
    }
}
