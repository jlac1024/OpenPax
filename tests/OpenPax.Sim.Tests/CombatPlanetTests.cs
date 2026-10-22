using OpenPax.Sim.Combat;
using OpenPax.Sim.Players;
using OpenPax.Sim.Setup;

namespace OpenPax.Sim.Tests;

public class CombatPlanetTests
{
    /// <summary>A human fleet attacks the computer's home colony, defended by one missile base with a sensor.</summary>
    private static (Game Game, Battle Battle, List<BattleShip> Human, Orbital Base) NewBattle(int count = 1)
    {
        var tables = GameTablesForTests.Load();
        var game = NewGame.Create(new GameSetup(1234, 30, 3, 3, [new HumanPlayerSetup("Commander", 0, 0, tables.Species[0])]), tables);
        var human = game.Players[0];
        var computer = game.Players[1];
        var ours = human.Fleets.First(f => f.Ships.Count > 0);
        var armed = human.Designs.OfType<Design>().OrderByDescending(d => d.Weapons.Count).First();
        for (var i = 0; i < 3; i++)
        {
            ours.AddShip(game.NewShip(armed), tables.Tech);
        }

        var colony = computer.Colonies[0];
        var missileBase = new Structure(tables.OfKind(TechInfo.MissileBaseKind).First(), colony) { Count = count };
        missileBase.HitPoints = missileBase.MaxHitPoints;
        missileBase.WeaponTechs[0] = tables.OfKind(TechInfo.MissileKind).First();
        missileBase.SensorTech = tables.OfKind(TechInfo.SensorKind).First(t => t.SensorTargets > 0);
        colony.Structures.RemoveAll(s => s.Tech.Kind is TechInfo.MissileBaseKind or TechInfo.BattleStationKind or TechInfo.FighterBaseKind);
        colony.Structures.Insert(0, missileBase);

        var events = StepEvents.None;
        game.SetTreaty(human, computer, Treaty.War, Treaty.InForce, ref events);
        game.SetTreaty(computer, human, Treaty.War, Treaty.InForce, ref events);
        game.StartPlanetBattle(ours, colony.Planet, ref events);
        game.FightStepByStep = true;
        var battle = game.Battles[0];
        var orbital = battle.Orbitals[computer.Index].Single(o => o.Structure == missileBase);
        return (game, battle, battle.Ships[human.Index], orbital);
    }

    private static Shot BigShot(Game game, int owner, BattleObject target, int rounds = 1)
    {
        var tech = game.Tables.OfKind(TechInfo.EnergyWeaponKind).First();
        var shot = new Shot(owner, tech, false) { Target = target, Rounds = rounds, Power = 1000000 };
        shot.SetPosition(target.PosX, target.PosY);
        return shot;
    }

    [Fact]
    public void BlocksStandRoundThePlanetInTheFarCorner()
    {
        var (game, battle, _, _) = NewBattle();
        Assert.True(game.Tables.CombatPictureSizes.ContainsKey(20100));
        Assert.True(game.Tables.CombatPictureSizes.ContainsKey(20209));
        Assert.SkipWhen(battle.Blocks.Count == 0, "The colony has no improvement under way.");
        Assert.Equal(battle.Bands + 1, battle.Blocks.Count);
        Assert.Equal(battle.Blocks.Count - 1, battle.Blocks[0].Index);
        Assert.Equal(battle.Blocks.Count, battle.Blocks.Select(b => b.Id).Distinct().Count());
        Assert.NotNull(battle.PlanetObject);
        Assert.All(battle.Blocks, b =>
        {
            Assert.InRange(b.X, 3300, 3600);
            Assert.InRange(b.Y, 3200, 3700);
            Assert.True(b.Id > battle.PlanetObject.Id);
        });
    }

    [Fact]
    public void AnOrbitalFollowsTheShipNearestTheCornerAndFiresFullVolleys()
    {
        var (game, battle, human, orbital) = NewBattle();
        Assert.True(orbital.TrackLimit > 0);

        // Ships more than 2500 from the corner are followed but never shot at.
        var nearest = human[^1];
        nearest.SetPosition(2400.0, 2600.0);
        game.Step();
        Assert.Contains(nearest, orbital.Tracked);
        // Ships fire at their condition, 100 at most; only orbitals fire at 150.
        var shot = Assert.Single(battle.Missiles[orbital.Owner].Concat(battle.Shots[orbital.Owner]), s => s.Power == 150);
        Assert.Equal(4, shot.Rounds);
        Assert.Equal(100, shot.Accuracy);
        Assert.Same(nearest, shot.Target);
        Assert.Same(nearest, orbital.LastTarget);
    }

    [Fact]
    public void AVolleyIsOneShotMoreThanTheStackThenAHalfReload()
    {
        var (_, _, _, orbital) = NewBattle(count: 2);
        var s = orbital.Structure;
        Assert.Equal(2, s.VolleyLeft[0]);
        s.TakeVolley(0);
        s.TakeVolley(0);
        Assert.Equal(0, s.Reload[0]);
        s.TakeVolley(0);
        Assert.Equal(2, s.VolleyLeft[0]);
        Assert.Equal(s.WeaponTechs[0]!.Reload >> 1, s.Reload[0]); // fitted in the setup
    }

    [Fact]
    public void AStructureDestroyedCreditsTheShooterAndExplodes()
    {
        var (game, battle, human, orbital) = NewBattle();
        var owner = human[0].Owner;
        var commander = battle.Commanders[owner]!; // the human side has a commander
        var footprint = battle.Grid[Battle.Footprint(orbital.X, orbital.Y, (orbital.Size + 1) / 2) is var f ? f.X0 * Battle.GridSize + f.Y0 : 0];
        Assert.NotEqual(0, footprint);
        orbital.Incoming.Add(BigShot(game, owner, orbital));
        var events = StepEvents.None;
        game.ResolveOrbitalHits(battle, orbital, ref events);
        Assert.True(orbital.Dead);
        Assert.DoesNotContain(orbital, battle.Orbitals[orbital.Owner]);
        Assert.Equal(250, commander.Kills);
        Assert.Equal(battle.Steps, battle.LastHit);
        Assert.Contains(battle.OrbitalEffects, e => e.Kind == 6 && e.Life == 35 && e.Follow is null);
        var flash = Assert.Single(battle.OrbitalEffects, e => e.Follow == orbital);
        Assert.Equal(0, battle.Grid[Battle.Footprint(orbital.X, orbital.Y, (orbital.Size + 1) / 2) is var g ? g.X0 * Battle.GridSize + g.Y0 : 0]);

        // A flash riding on something dead goes at the next move; the explosion stays.
        game.Step();
        Assert.DoesNotContain(flash, battle.OrbitalEffects);
        Assert.Contains(battle.OrbitalEffects, e => e.Kind == 6 && e.Age == 1);
    }

    [Fact]
    public void ShieldFlashesLastTenStepsAndExplosionsThirtyFive()
    {
        Assert.Equal(10, Game.EffectLife(11));
        Assert.Equal(10, Game.EffectLife(130));
        Assert.Equal(35, Game.EffectLife(10));
        Assert.Equal(35, Game.EffectLife(131));
        Assert.Equal(39, Game.EffectLife(143));
    }

    [Fact]
    public void BombsTakePopulationAndTheTopBlockFalls()
    {
        var (game, battle, human, _) = NewBattle();
        Assert.SkipWhen(battle.Blocks.Count == 0, "The colony has no improvement under way.");
        var colony = battle.Planet!.Colony!; // a planet battle at a colony
        var population = colony.CurrentPopulation;
        var top = battle.Blocks[0];
        var bomb = game.Tables.Techs.First(t => t.Kind == TechInfo.PlanetBombKind);
        var shot = new Shot(human[0].Owner, bomb, false) { Target = top, Rounds = 1, Power = 1000 };
        shot.SetPosition(top.PosX, top.PosY);
        top.Incoming.Add(shot);
        var events = StepEvents.None;
        game.ResolveBlockHits(battle, top, ref events);
        Assert.Equal(battle.Steps, battle.LastHit);
        Assert.True(colony.Destroyed || colony.CurrentPopulation < population);
        Assert.True(shot.Dead);
        Assert.Empty(top.Incoming);
        if (top.Dead)
        {
            Assert.Equal((top.X, top.Y), (battle.PlanetObject!.X, battle.PlanetObject.Y)); // a planet battle has its planet object
            Assert.Contains(battle.OrbitalEffects, e => e.Follow == battle.PlanetObject && e.Kind >= 135);
        }
        else
        {
            Assert.Contains(battle.OrbitalEffects, e => e.Follow == top && e.Kind == 131);
        }
    }

    [Fact]
    public void AShipWithABombFiresOneRoundOfItsWholeVolleyAtTheTopBlock()
    {
        var (game, battle, human, _) = NewBattle();
        Assert.SkipWhen(battle.Blocks.Count == 0, "The colony has no improvement under way.");
        var ship = human[0];
        var bomb = game.Tables.Techs.First(t => t.Kind == TechInfo.PlanetBombKind);
        ship.Ship.Add(bomb, 0);
        var top = battle.Blocks[0];
        ship.SetPosition(top.PosX - 200.0, top.PosY - 200.0);
        game.FireShip(battle, ship);
        var shot = Assert.Single(battle.Shots[ship.Owner].Concat(battle.Missiles[ship.Owner]), s => s.Target == top);
        Assert.Equal(1, shot.Rounds);
        Assert.Equal(battle.Steps, battle.LastShot);
        Assert.True(shot.Power >= 100);
    }

    [Fact]
    public void OrbitalOrdersSetItsTargetAndItsLaunching()
    {
        var (game, battle, human, orbital) = NewBattle();
        game.ApplyBattleOrder(new BattleOrder(BattleOrderKind.Target, battle.Id, human[1].Id, orbital.Id, 0, 0, []));
        Assert.Same(human[1], orbital.Ordered);
        Assert.True(orbital.AutoLaunch);
        game.ApplyBattleOrder(new BattleOrder(BattleOrderKind.RecallFighter, battle.Id, -1, orbital.Id, 0, 0, []));
        Assert.False(orbital.AutoLaunch);
        game.ApplyBattleOrder(new BattleOrder(BattleOrderKind.Attack, battle.Id, 999999, orbital.Id, 0, 0, []));
        Assert.Same(human[1], orbital.Ordered);
    }

    [Fact]
    public void TheSamePlanetBattleRunsTheSameWay()
    {
        static string Run()
        {
            var (game, battle, human, orbital) = NewBattle(count: 3);
            game.ApplyBattleOrder(new BattleOrder(BattleOrderKind.Attack, battle.Id, orbital.Id, -1, 0, 0, [.. human.Select(s => s.Id)]));
            for (var i = 0; i < 1500 && game.Battles.Contains(battle); i++)
            {
                game.Step();
            }

            return game.Random.State + "|" + orbital.Structure.HitPoints + "|" + battle.LastHit + "|"
                + string.Join(";", human.Select(s => $"{s.Id}:{s.PosX}:{s.PosY}:{s.Ship.Structure}:{s.Dead}"));
        }

        Assert.Equal(Run(), Run());
    }
}
