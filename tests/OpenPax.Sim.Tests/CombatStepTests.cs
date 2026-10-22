using OpenPax.Sim.Combat;
using OpenPax.Sim.Players;
using OpenPax.Sim.Setup;

namespace OpenPax.Sim.Tests;

public class CombatStepTests
{
    /// <summary>A game with a human, and a battle between the human's fleet and a computer player's.</summary>
    private static (Game Game, Battle Battle, List<BattleShip> Human, List<BattleShip> Computer) NewBattle(int extraShips = 0)
    {
        var tables = GameTablesForTests.Load();
        var game = NewGame.Create(new GameSetup(1234, 30, 3, 3, [new HumanPlayerSetup("Commander", 0, 0, tables.Species[0])]), tables);
        var human = game.Players[0];
        var computer = game.Players[1];
        var ours = human.Fleets.First(f => f.Ships.Count > 0);
        var theirs = computer.Fleets.First(f => f.Ships.Count > 0);
        for (var i = 0; i < extraShips; i++)
        {
            ours.AddShip(game.NewShip(human.Designs.OfType<Design>().First(d => d.CombatSpeed() > 0.0)), tables.Tech);
        }

        game.StartFleetBattle(ours, theirs);
        game.FightStepByStep = true;
        var battle = game.Battles[0];

        // These tests drive movement and orders alone: the computer side is
        // held still (its thinking is tested in CombatThinkingTests).
        battle.Commanders[computer.Index]!.Computer = false; // both sides have a commander
        return (game, battle, battle.Ships[human.Index], battle.Ships[computer.Index]);
    }

    private static void Steps(Game game, int n)
    {
        for (var i = 0; i < n && game.InCombat; i++)
        {
            game.Step();
        }
    }

    [Fact]
    public void ShipsArePlacedWithExactPositionsAndIds()
    {
        var (_, battle, human, computer) = NewBattle();
        Assert.True(battle.Humans > 0);
        Assert.All(human.Concat(computer), s =>
        {
            Assert.True(s.Id > 0);
            Assert.Equal((double)s.X, s.PosX);
            Assert.True(s.TopSpeed >= 0.0);
            Assert.True(s.TurnRate is 11.25 or 5.625 or 3.75);
        });
        Assert.Equal(human.Concat(computer).Count(), human.Concat(computer).Select(s => s.Id).Distinct().Count());
    }

    [Fact]
    public void TurningLeftTakesADegreeMore()
    {
        var (_, _, human, _) = NewBattle();
        var ship = human.First(s => s.TurnRate == 11.25);
        ship.Facing = 100;
        ship.WantedHeading = 150;
        Game.Turn(ship);
        Assert.Equal(111, ship.Facing);
        ship.Facing = 100;
        ship.WantedHeading = 50;
        Game.Turn(ship);
        Assert.Equal(88, ship.Facing);
    }

    [Fact]
    public void AMovedShipTurnsThenGoesAndStopsAtItsPoint()
    {
        var (game, battle, human, _) = NewBattle();
        var ship = human.First(s => s.TopSpeed > 0.0);
        var to = (X: Math.Clamp(ship.X - 600, 200, 3400), Y: Math.Clamp(ship.Y + 400, 200, 3400));
        game.ApplyBattleOrder(new BattleOrder(BattleOrderKind.Face, battle.Id, -1, -1, to.X, to.Y, [ship.Id]));
        Assert.Equal(BattleShip.Turning, ship.State);

        game.ApplyBattleOrder(new BattleOrder(BattleOrderKind.Move, battle.Id, -1, -1, to.X, to.Y, [ship.Id]));
        Assert.Equal(BattleShip.Moving, ship.State);
        Steps(game, 600);
        Assert.Equal(BattleShip.Idle, ship.State);
        Assert.True(Math.Abs(ship.X - to.X) <= ship.Radius && Math.Abs(ship.Y - to.Y) <= ship.Radius, $"{ship.X},{ship.Y} vs {to}");
    }

    [Fact]
    public void AGroupMoveSendsEveryShipToItsOwnSlot()
    {
        var (game, battle, human, _) = NewBattle(extraShips: 6);
        Assert.True(human.Count >= 7);
        // Open space: the enemy stands at the field's center.
        game.ApplyBattleOrder(new BattleOrder(BattleOrderKind.Move, battle.Id, -1, -1, 1000, 2400, [.. human.Select(s => s.Id)]));
        Assert.All(human, s => Assert.Equal(BattleShip.Moving, s.State));
        var slots = human.Select(s => s.Goal!.Point).ToList(); // every ship was just sent, so each has a goal
        Assert.Equal(slots.Count, slots.Distinct().Count());

        // They get there without running into each other.
        Steps(game, 1500);
        Assert.All(human, s => Assert.Equal(BattleShip.Idle, s.State));
        Assert.All(human, s => Assert.True(Math.Abs(s.X - 1000) < 400 && Math.Abs(s.Y - 2400) < 400, $"{s.X},{s.Y}"));
    }

    [Fact]
    public void LinesPutShipsAHundredApart()
    {
        var (game, battle, human, _) = NewBattle(extraShips: 4);
        var ids = human.Select(s => s.Id).ToList();
        game.ApplyBattleOrder(new BattleOrder(BattleOrderKind.Horizontal, battle.Id, -1, -1, 1000, 2400, ids));
        var across = human.Select(s => s.Goal!.Point).OrderBy(p => p.X).ToList(); // each was just sent
        Assert.All(across, p => Assert.Equal(2400.0, p.Y));
        Assert.Equal(Enumerable.Range(0, across.Count).Select(i => across[0].X + 100 * i), across.Select(p => p.X));

        game.ApplyBattleOrder(new BattleOrder(BattleOrderKind.Vertical, battle.Id, -1, -1, 1000, 2400, ids));
        var down = human.Select(s => s.Goal!.Point).OrderBy(p => p.Y).ToList(); // each was just sent
        Assert.All(down, p => Assert.Equal(1000.0, p.X));

        // With no ships chosen, the Horizontal order is AUTO for the side of the ship named.
        game.ApplyBattleOrder(new BattleOrder(BattleOrderKind.Horizontal, battle.Id, human[0].Id, -1, 0, 0, []));
        Assert.True(battle.Commanders[human[0].Owner]!.Computer); // the human side has a commander
        Assert.True(battle.AutoEngaged);
    }

    [Fact]
    public void ARetreatingShipLeavesByTheEdgeAndItsFleetRetreats()
    {
        var (game, battle, human, _) = NewBattle();
        var ship = human.First(s => s.TopSpeed > 0.0);
        game.ApplyBattleOrder(new BattleOrder(BattleOrderKind.Retreat, battle.Id, -1, -1, 0, 0, [ship.Id]));
        Assert.Equal(BattleShip.Leaving, ship.State);
        Steps(game, 2000);
        Assert.True(ship.Dead);
        Assert.Contains(ship, battle.Graveyard);
        Assert.DoesNotContain(ship, human);
    }

    [Fact]
    public void AQuietBattleEndsAfterFifteenHundredStepsOfNothing()
    {
        var (game, battle, _, _) = NewBattle();
        Steps(game, 1400);
        Assert.Contains(battle, game.Battles);
        Steps(game, 200);
        Assert.DoesNotContain(battle, game.Battles);
        Assert.Equal(1501, battle.Steps);
    }

    [Fact]
    public void TheSameOrdersGiveTheSameBattle()
    {
        static string Run()
        {
            var (game, battle, human, computer) = NewBattle();
            game.ApplyBattleOrder(new BattleOrder(BattleOrderKind.Attack, battle.Id, computer[0].Id, -1, 0, 0, [.. human.Select(s => s.Id)]));
            Steps(game, 400);
            return string.Join(";", human.Concat(computer).Select(s => $"{s.Id}:{s.PosX}:{s.PosY}:{s.Facing}:{s.State}"));
        }

        Assert.Equal(Run(), Run());
    }
}
