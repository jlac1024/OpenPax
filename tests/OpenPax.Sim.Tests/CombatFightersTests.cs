using OpenPax.Sim.Combat;
using OpenPax.Sim.Players;
using OpenPax.Sim.Setup;

namespace OpenPax.Sim.Tests;

public class CombatFightersTests
{
    /// <summary>
    /// Human carriers attack the computer's home colony, which has a fighter
    /// base and a minefield (and no other defense that acts).
    /// </summary>
    private static (Game Game, Battle Battle, List<BattleShip> Human, Orbital Base, Structure Field) NewBattle(int carriers = 3)
    {
        var tables = GameTablesForTests.Load();
        var game = NewGame.Create(new GameSetup(1234, 30, 3, 3, [new HumanPlayerSetup("Commander", 0, 0, tables.Species[0])]), tables);
        var human = game.Players[0];
        var computer = game.Players[1];
        var ours = human.Fleets.First(f => f.Ships.Count > 0);
        var carrier = human.Designs.OfType<Design>().OrderByDescending(d => d.Weapons.Count).First().Copy();
        carrier.Add(tables.OfKind(TechInfo.FighterKind).First(), Component.NotWeapon);
        carrier.Add(tables.OfKind(TechInfo.FighterKind).First(), Component.NotWeapon);
        for (var i = 0; i < carriers; i++)
        {
            ours.AddShip(game.NewShip(carrier), tables.Tech);
        }

        var colony = computer.Colonies[0];
        colony.Structures.RemoveAll(s => s.Tech.Kind is TechInfo.MissileBaseKind or TechInfo.BattleStationKind or TechInfo.FighterBaseKind or TechInfo.MineFieldKind);
        var fighterBase = new Structure(tables.OfKind(TechInfo.FighterBaseKind).First(), colony);
        fighterBase.HitPoints = fighterBase.MaxHitPoints;
        fighterBase.FighterTechs[0] = tables.OfKind(TechInfo.FighterKind).First();
        var field = new Structure(tables.OfKind(TechInfo.MineFieldKind).Last(), colony);
        field.HitPoints = field.MaxHitPoints;
        colony.Structures.Insert(0, fighterBase);
        colony.Structures.Insert(0, field);

        var events = StepEvents.None;
        game.SetTreaty(human, computer, Treaty.War, Treaty.InForce, ref events);
        game.SetTreaty(computer, human, Treaty.War, Treaty.InForce, ref events);
        game.StartPlanetBattle(ours, colony.Planet, ref events);
        game.FightStepByStep = true;
        var battle = game.Battles[0];
        var orbital = battle.Orbitals[computer.Index].Single(o => o.Structure == fighterBase);
        return (game, battle, battle.Ships[human.Index], orbital, field);
    }

    [Fact]
    public void CarriersBringTheirFightersDockedAtTheFieldsCorner()
    {
        var (_, battle, human, _, _) = NewBattle();
        var owner = human[0].Owner;
        foreach (var ship in human.Where(s => s.Ship.Fighters.Count > 0))
        {
            var mine = battle.Fighters[owner].Where(f => f.Carrier == ship).ToList();
            Assert.Equal(ship.Ship.Fighters.Sum(b => b.Operational), mine.Count);
            Assert.All(mine, f =>
            {
                Assert.True(f.Docked);
                Assert.Equal(Fighter.DockedState, f.State);
                Assert.Equal((0, 0), (f.X, f.Y));
                Assert.Equal(3, f.Craft);
                Assert.Equal(f.Tech.FighterHitPoints / 3, f.HitPoints);
                Assert.True(f.Id > ship.Id);
            });
        }
    }

    [Fact]
    public void AFighterBaseHasOneMoreRoundOfFightersThanTwiceItsCount()
    {
        var (_, battle, _, orbital, _) = NewBattle();
        var fighters = battle.Fighters[orbital.Owner].Where(f => f.Carrier == orbital).ToList();
        Assert.Equal(3, fighters.Count);
        Assert.All(fighters, f => Assert.Equal((orbital.X, orbital.Y), (f.X, f.Y)));
    }

    [Fact]
    public void MinesLieInARingRoundThePlanetsCorner()
    {
        var (_, battle, _, orbital, field) = NewBattle();
        var mines = battle.Mines[orbital.Owner];
        Assert.Equal(75, mines.Count);
        Assert.Equal(75, field.MinesDeployed);
        Assert.All(mines, m =>
        {
            var d = Math.Sqrt((3600.0 - m.X) * (3600.0 - m.X) + (3600.0 - m.Y) * (3600.0 - m.Y));
            Assert.InRange(d, 700.0, 2000.5);
            Assert.Equal((m.X, m.Y), (m.HomeX, m.HomeY));
            Assert.Equal(field.Tech.InstallationStrength, m.Damage);
            Assert.Equal(3, m.Level);
        });
    }

    [Fact]
    public void ACarrierLaunchesTwoFightersAtAFollowedEnemy()
    {
        var (game, battle, human, orbital, _) = NewBattle();
        var ship = human.First(s => s.Ship.Fighters.Count > 0);
        ship.Tracked.Clear();
        ship.Tracked.Add(orbital);
        ship.LaunchCountdown = 0;
        var draws = game.Random.State;
        game.FireShip(battle, ship);
        var launched = battle.Fighters[ship.Owner].Where(f => f.Carrier == ship && !f.Docked).ToList();
        Assert.Equal(2, launched.Count);
        Assert.All(launched, f =>
        {
            Assert.Equal(Fighter.Attacking, f.State);
            Assert.Same(orbital, f.Link);
            Assert.Equal((ship.X, ship.Y), (f.X, f.Y));
        });
        Assert.Equal(5, ship.LaunchCountdown);
        Assert.NotEqual(draws, game.Random.State);
    }

    [Fact]
    public void LaunchedFightersCloseInAndFireAtHalfPower()
    {
        var (game, battle, human, orbital, _) = NewBattle();
        var ship = human.First(s => s.Ship.Fighters.Count > 0);
        game.ApplyBattleOrder(new BattleOrder(BattleOrderKind.FightersAttack, battle.Id, orbital.Id, -1, 0, 0, [ship.Id]));
        var launched = battle.Fighters[ship.Owner].Where(f => !f.Docked).ToList();
        Assert.NotEmpty(launched);
        var fired = false;
        for (var i = 0; i < 1500 && !fired && game.Battles.Contains(battle); i++)
        {
            game.Step();
            fired = battle.Shots[ship.Owner].Concat(battle.Missiles[ship.Owner]).Any(s => s.Power == 50);
        }

        Assert.True(fired, "no fighter shot");
    }

    [Fact]
    public void ARecalledFighterComesHomeAndDocks()
    {
        var (game, battle, human, orbital, _) = NewBattle();
        var ship = human.First(s => s.Ship.Fighters.Count > 0);
        game.ApplyBattleOrder(new BattleOrder(BattleOrderKind.FightersAttack, battle.Id, orbital.Id, -1, 0, 0, [ship.Id]));
        for (var i = 0; i < 100; i++)
        {
            game.Step();
        }

        var f = battle.Fighters[ship.Owner].First(f => f.Carrier == ship && !f.Docked);
        game.ApplyBattleOrder(new BattleOrder(BattleOrderKind.RecallFighter, battle.Id, -1, -1, 0, 0, [ship.Id]));
        Assert.Equal(Fighter.Returning, f.State);
        for (var i = 0; i < 2000 && !f.Docked && !f.Dead && game.Battles.Contains(battle); i++)
        {
            game.Step();
        }

        Assert.True(f.Docked || f.Dead);
    }

    [Fact]
    public void WhenTheCarrierDiesItsDockedFightersVanishAndTheRestFlyOn()
    {
        var (game, battle, human, orbital, _) = NewBattle();
        var ship = human.First(s => s.Ship.Fighters.Count > 0);
        ship.Tracked.Clear();
        ship.Tracked.Add(orbital);
        ship.LaunchCountdown = 0;
        game.FireShip(battle, ship);
        var flying = battle.Fighters[ship.Owner].Where(f => f.Carrier == ship && !f.Docked).ToList();
        var docked = battle.Fighters[ship.Owner].Count(f => f.Carrier == ship && f.Docked);
        var before = battle.Fighters[ship.Owner].Count;
        ship.Ship.Structure = 1;
        var h = new Shot(orbital.Owner, game.Tables.OfKind(TechInfo.EnergyWeaponKind).First(), false) { Target = ship, Rounds = 1, Power = 100000 };
        ship.Incoming.Add(h);
        var events = StepEvents.None;
        game.ResolveShipHits(battle, ship, ref events);
        Assert.True(ship.Dead);
        Assert.Equal(before - docked, battle.Fighters[ship.Owner].Count);
        Assert.All(flying, f => Assert.Null(f.Carrier));
    }

    [Fact]
    public void AShipTouchingAMineTakesItsDamageAndTheFieldWearsDown()
    {
        var (game, battle, human, orbital, field) = NewBattle();
        var ship = human[0];
        var mine = battle.Mines[orbital.Owner][0];
        ship.SetPosition(mine.PosX, mine.PosY);
        var structure = ship.Ship.Structure;
        var hp = field.HitPoints;
        var events = StepEvents.None;
        game.ResolveShipHits(battle, ship, ref events);
        Assert.True(mine.Dead);
        Assert.DoesNotContain(mine, battle.Mines[orbital.Owner]);
        Assert.True(ship.Dead || ship.Ship.Structure <= structure - mine.Damage);
        Assert.True(field.HitPoints < hp);
        Assert.True(field.MinesDeployed < 75);
        Assert.Contains(battle.ShipEffects, e => e.Kind == 3);
    }

    [Fact]
    public void EnemyMinesAreSeenOnlyWithAGoodEnoughMinesweeper()
    {
        var (game, battle, human, orbital, _) = NewBattle();
        var owner = human[0].Owner;
        Assert.False(game.MinesVisible(battle, orbital.Owner, owner));
        Assert.True(game.MinesVisible(battle, orbital.Owner, orbital.Owner));
        human[0].Ship.Add(game.Tables.OfKind(TechInfo.MinesweeperKind).Last(), Component.NotWeapon);
        Assert.True(game.MinesVisible(battle, orbital.Owner, owner));
    }

    [Fact]
    public void TheSameBattleWithFightersAndMinesRunsTheSameWay()
    {
        static string Run()
        {
            var (game, battle, human, orbital, field) = NewBattle();
            game.ApplyBattleOrder(new BattleOrder(BattleOrderKind.Attack, battle.Id, orbital.Id, -1, 0, 0, [.. human.Select(s => s.Id)]));
            for (var i = 0; i < 1500 && game.Battles.Contains(battle); i++)
            {
                game.Step();
            }

            return game.Random.State + "|" + field.HitPoints + "|" + battle.LastHit + "|"
                + string.Join(";", battle.Fighters.SelectMany(l => l).Select(f => $"{f.Id}:{f.PosX}:{f.PosY}:{f.Craft}:{f.State}"))
                + string.Join(";", human.Select(s => $"{s.Id}:{s.PosX}:{s.PosY}:{s.Ship.Structure}:{s.Dead}"));
        }

        Assert.Equal(Run(), Run());
    }
}
