using OpenPax.Sim.Galaxy;
using OpenPax.Sim.Players;
using OpenPax.Sim.Setup;

namespace OpenPax.Sim.Tests;

/// <summary>The fixes to the original's bugs (docs/sim/fix-candidates.md): each off plays as the original, on as fixed.</summary>
public sealed class FixesTests
{
    private static (Game Game, Player Human, Player Computer) NewGame(Fixes fixes = Fixes.None)
    {
        var tables = GameTablesForTests.Load();
        var game = Setup.NewGame.Create(new GameSetup(1234, 30, 2, 3, [new HumanPlayerSetup("Commander", 0, 0, tables.Species[0])]), tables);
        game.Fixes = fixes;
        return (game, game.Players[0], game.Players[1]);
    }

    [Fact]
    public void AGameWithNoFixesKeepsItsHash()
    {
        var (plain, _, _) = NewGame();
        var before = plain.Hash();
        plain.Fixes = Fixes.None;
        Assert.Equal(before, plain.Hash());
        plain.Fixes = Fixes.ExperienceOnce;
        Assert.NotEqual(before, plain.Hash());
    }

    /// <summary>A fleet of one ship of the player's, out of orbit at a point in the star of a fleet.</summary>
    private static Fleet Detach(Game game, Player owner, Fleet from, Star star, int x, int y)
    {
        var ship = from.Ships[^1];
        from.RemoveShip(ship, game.Tables.Tech);
        var fleet = game.CreateFleet(owner, star, star.Planets[0]);
        fleet.AddShip(ship, game.Tables.Tech);
        fleet.LeaveOrbit();
        fleet.MoveTo(x, y);
        return fleet;
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void AFleetNearAFleetBattleJoinsOnlyWithTheFix(bool fixedGame)
    {
        var (game, human, computer) = NewGame(fixedGame ? Fixes.FleetsJoinBattles : Fixes.None);
        var events = StepEvents.None;
        game.SetTreaty(human, computer, Treaty.War, Treaty.InForce, ref events);
        game.SetTreaty(computer, human, Treaty.War, Treaty.InForce, ref events);
        var own = human.Fleets.Where(f => f.Ships.Count > 0).Take(2).ToList();
        var star = game.Universe.Stars[own[0].Star];
        var target = Detach(game, computer, computer.Fleets.First(f => f.Ships.Count > 0), star, 200, 200);
        var attacker = Detach(game, human, own[0], star, 230, 200);
        var joiner = Detach(game, human, own[1], star, 200, 170); // heading for the target from another side: its slot is free

        game.StartFleetBattle(attacker, target);
        Assert.Equal(fixedGame, joiner.InCombat);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void StopWorkSparesADamagedStructureOnlyWithTheFix(bool fixedGame)
    {
        var (game, human, _) = NewGame(fixedGame ? Fixes.ScrapUnderWork : Fixes.None);
        var colony = human.Colonies[0];
        foreach (var yard in colony.Structures.Where(s => s.Tech.Kind == TechInfo.ShipyardKind))
        {
            yard.Queue.Clear();
        }

        var damaged = colony.Structures[0];
        damaged.HitPoints = damaged.MaxHitPoints / 2;
        damaged.UnderWork = false;
        var units = colony.Structures.Sum(s => s.Count);
        game.StopWork(colony.Planet);
        Assert.Equal(fixedGame ? units : units - 1, colony.Structures.Sum(s => s.Count));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void TheColonizeKeyNeedsAColonyShipOnlyWithTheFix(bool fixedGame)
    {
        var (game, human, _) = NewGame(fixedGame ? Fixes.ColonizeNeedsShip : Fixes.None);
        var fleet = human.Fleets.First(f => f.Ships.Count > 0 && !Game.HasColonyShip(f));
        var star = game.Universe.Stars[fleet.Star];
        var empty = star.Planets.First(p => p.Colony is null);
        game.ColonizeNow(human, empty, fleet);
        Assert.Equal(!fixedGame, empty.Colony is not null);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void ARefitAtAForeignYardIsTakenOnlyWithoutTheFix(bool fixedGame)
    {
        var (game, human, computer) = NewGame(fixedGame ? Fixes.OwnYardRefits : Fixes.None);
        var fleet = human.Fleets.First(f => f.Ships.Count > 0);
        fleet.EnterOrbit(game.Random, computer.Colonies[0].Planet);
        Assert.True(Game.AtShipyard(fleet));
        var ship = fleet.Ships[0];
        game.MarkRefit(human, ship.ShipId, human.Designs.OfType<Design>().First().Slot);
        Assert.Equal(!fixedGame, ship.Refit is not null);
    }

    [Fact]
    public void AGameWithEveryFixPlaysTheSameEachTime()
    {
        var all = Enum.GetValues<Fixes>().Aggregate(Fixes.None, (a, f) => a | f);
        ulong Run()
        {
            var (game, _, _) = NewGame(all);
            for (var i = 0; i < 1500; i++)
            {
                game.Step();
            }

            return game.Hash();
        }

        Assert.Equal(Run(), Run());
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void ATradeOfMoreShipsThanAFleetHoldsLosesNoneWithTheFix(bool fixedGame)
    {
        var (game, human, computer) = NewGame(fixedGame ? Fixes.TradeKeepsAllShips : Fixes.None);
        var source = computer.Fleets.First(f => f.Ships.Count > 0);
        var star = game.Universe.Stars[source.Star];
        var design = source.Ships[0];
        var package = new TradePackage(computer, human);
        for (var n = 0; n < 25; n++)
        {
            var fleet = game.CreateFleet(computer, star, star.Planets[0]);
            var ship = game.NewShip(design);
            fleet.AddShip(ship, game.Tables.Tech);
            package.Offered.Ships.Add(ship.ShipId);
        }

        var before = human.Fleets.Sum(f => f.Ships.Count);
        game.AnswerPackage(package, accept: true);
        Assert.Equal(before + (fixedGame ? 25 : Fleet.MaxShips), human.Fleets.Sum(f => f.Ships.Count));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void AnEmpireThatNeverMetTheVictimHoldsNothingAgainstTheAttackerWithTheFix(bool fixedGame)
    {
        var tables = GameTablesForTests.Load();
        var game = Setup.NewGame.Create(new GameSetup(1234, 30, 3, 3, [new HumanPlayerSetup("Commander", 0, 0, tables.Species[0])]), tables);
        game.Fixes = fixedGame ? Fixes.StrangersIndifferent : Fixes.None;
        var (attacker, victim, stranger) = (game.Players[0], game.Players[1], game.Players[2]);
        Assert.Equal(Treaty.None, victim.Treaties[stranger.Index].Level());
        var before = stranger.RelationScores[attacker.Index];
        typeof(Game).GetMethod("ReactToAttack", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)!.Invoke(game, [attacker, victim]);
        Assert.Equal(fixedGame, stranger.RelationScores[attacker.Index] == before);
    }
}
