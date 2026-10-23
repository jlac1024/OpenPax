using OpenPax.Data.Install;
using OpenPax.Data.Tables;
using OpenPax.Host.Headless;
using OpenPax.Sim;
using OpenPax.Sim.Galaxy;
using OpenPax.Sim.Players;
using OpenPax.Sim.Setup;
using OpenPax.TestSupport;

namespace OpenPax.Modding.Tests;

/// <summary>The Simple Combat sample mod (samples/SimpleCombat), installed into a mods folder and loaded as the game loads it.</summary>
public sealed class SimpleCombatTests : IDisposable
{
    private static GameTables? _tables;
    private readonly TempFolder _mods = new();

    public void Dispose() => _mods.Dispose();

    private static GameTables Tables()
    {
        var folder = GameData.RequireFolder();
        return _tables ??= GameTablesLoader.Load(
            Path.Combine(folder, "Data"),
            DirectionTables.FromExecutable(File.ReadAllBytes(Path.Combine(folder, GameInstall.Executable))),
            SpriteRows.Measure(OpenPax.Data.Resources.ResourceFile.Load(Path.Combine(folder, "Image", "Combat.img"))));
    }

    /// <summary>The sample as build.sh installs it: its built files in a folder of the mods folder.</summary>
    private ModHost Install(string? settings = null)
    {
        // The sample builds next to these tests under build/artifacts/bin, in a folder of its own name.
        var built = AppContext.BaseDirectory.Replace("OpenPax.Modding.Tests", "SimpleCombat", StringComparison.Ordinal);
        var folder = Path.Combine(_mods.Path, "simple-combat");
        Directory.CreateDirectory(folder);
        foreach (var name in new[] { "SimpleCombat.dll", "mod.toml", "simple-combat.toml" })
        {
            File.Copy(Path.Combine(built, name), Path.Combine(folder, name), true);
        }

        if (settings is not null)
        {
            File.WriteAllText(Path.Combine(folder, "simple-combat.toml"), settings);
        }

        var host = ModHost.Load(_mods.Path, ["simple-combat"]);
        Assert.Empty(host.Errors);
        return host;
    }

    private (Game Game, Player Human, Player Computer) NewGame(bool war = true, string? settings = null)
    {
        var host = Install(settings);
        var tables = Tables();
        var game = OpenPax.Sim.Setup.NewGame.Create(new GameSetup(1234, 30, 2, 3, [new HumanPlayerSetup("Commander", 0, 0, tables.Species[0])]), tables);
        host.NewGame(game);
        if (!war)
        {
            return (game, game.Players[0], game.Players[1]);
        }

        var events = StepEvents.None;
        game.SetTreaty(game.Players[0], game.Players[1], Treaty.War, Treaty.InForce, ref events);
        game.SetTreaty(game.Players[1], game.Players[0], Treaty.War, Treaty.InForce, ref events);
        return (game, game.Players[0], game.Players[1]);
    }

    /// <summary>A fleet of the owner's in a star, its ships taken from the owner's first fleets.</summary>
    private static Fleet Gather(Game game, Player owner, Star star, Planet near, int ships)
    {
        var fleet = game.CreateFleet(owner, star, near);
        foreach (var from in owner.Fleets.Where(f => f != fleet).ToList())
        {
            foreach (var ship in from.Ships.ToList())
            {
                if (fleet.Ships.Count >= ships)
                {
                    return fleet;
                }

                from.RemoveShip(ship, game.Tables.Tech);
                fleet.AddShip(ship, game.Tables.Tech);
            }
        }

        return fleet;
    }

    /// <summary>Every fleet of the player's in a star leaves for its far corner, so none defends a planet there.</summary>
    private static void SendAway(Player owner, Star star)
    {
        foreach (var f in owner.Fleets.Where(f => f.Star == star.Index))
        {
            f.LeaveOrbit();
            f.MoveTo(0, 0);
        }
    }

    [Fact]
    public void DefendingFleetsTakeTheBlowsFirst()
    {
        var (game, human, computer) = NewGame();
        var colony = computer.Colonies[0];
        var star = game.Universe.Stars[colony.Star];
        colony.Structures.RemoveAll(s => s.Tech.Kind is >= 7 and <= 10);
        SendAway(computer, star);
        var defender = Gather(game, computer, star, colony.Planet, 20);
        var besieger = Gather(game, human, star, colony.Planet, 20);
        var defense = defender.Ships.Sum(s => s.Structure);
        var population = colony.CurrentPopulation;
        Assert.True(defender.Ships.Count > 0 && besieger.Ships.Count > 0);

        var events = StepEvents.None;
        game.StartPlanetBattle(besieger, colony.Planet, ref events);
        Assert.Contains(new ModFight(defender, colony.Planet), game.Fights(star));
        for (var i = 0; i < 10; i++)
        {
            game.Step();
        }

        Assert.True(defender.Ships.Count == 0 || defender.Ships.Sum(s => s.Structure) < defense);
        if (defender.Ships.Count > 0)
        {
            Assert.Equal(population, colony.CurrentPopulation, 0.5); // the people are spared while the fleet stands
        }
    }

    [Fact]
    public void AFleetFlyingIntoOrbitJoinsTheSiege()
    {
        var (game, human, computer) = NewGame();
        var colony = computer.Colonies[0];
        var star = game.Universe.Stars[colony.Star];
        colony.Structures.RemoveAll(s => s.Tech.Kind is >= 7 and <= 10); // nothing to shoot back, so both fleets last
        SendAway(computer, star);
        var first = Gather(game, human, star, colony.Planet, 2);
        var events = StepEvents.None;
        game.StartPlanetBattle(first, colony.Planet, ref events);

        var second = Gather(game, human, star, colony.Planet, 2); // a human's new fleet circles the planet it is made at
        Assert.Equal(colony.Planet, second.OrbitCenter);
        for (var i = 0; i < 5; i++)
        {
            game.Step();
        }

        Assert.Contains(new ModFight(second, colony.Planet), game.Fights(star));
    }

    /// <summary>A siege of a colony held only by a mine field: what the besiegers lost, and what the field lost.</summary>
    private (int StructureLost, double FieldLost) MinedSiege(bool sweepers)
    {
        var (game, human, computer) = NewGame();
        var colony = computer.Colonies[0];
        var star = game.Universe.Stars[colony.Star];
        colony.Structures.RemoveAll(s => s.Tech.Kind is >= 7 and <= 10);
        var field = new Structure(game.Tables.OfKind(TechInfo.MineFieldKind).Last(), colony);
        field.HitPoints = field.MaxHitPoints;
        colony.Structures.Insert(0, field);
        SendAway(computer, star);
        var fleet = Gather(game, human, star, colony.Planet, 20);
        if (sweepers)
        {
            foreach (var ship in fleet.Ships)
            {
                ship.Add(game.Tables.OfKind(TechInfo.MinesweeperKind).Last(), Component.NotWeapon);
            }
        }

        var before = fleet.Ships.Sum(s => s.Structure);
        var events = StepEvents.None;
        game.StartPlanetBattle(fleet, colony.Planet, ref events);
        for (var i = 0; i < 25; i++)
        {
            game.Step();
        }

        return (before - fleet.Ships.Sum(s => s.Structure), field.MaxHitPoints - (colony.Structures.Contains(field) ? field.HitPoints : 0));
    }

    [Fact]
    public void MinesHitHardUntilSweptAndOnlySweepersClearThem()
    {
        var blind = MinedSiege(sweepers: false);
        var swept = MinedSiege(sweepers: true);
        Assert.True(blind.StructureLost > 0);
        Assert.True(swept.StructureLost < blind.StructureLost / 2, $"{blind} {swept}");
        Assert.True(swept.FieldLost > blind.FieldLost, $"{blind} {swept}");
    }

    [Fact]
    public void TheOddsFavorAStrongFleetOverABareColonyAndNotTheOtherWayRound()
    {
        var (game, human, computer) = NewGame();
        var colony = computer.Colonies[0];
        var star = game.Universe.Stars[colony.Star];
        var fleet = Gather(game, human, star, colony.Planet, 20);
        Assert.NotNull(game.RateAttack);
        var defended = game.RateAttack(game, fleet, colony.Planet);

        colony.Structures.RemoveAll(s => s.Tech.Kind is >= 7 and <= 10);
        SendAway(computer, star);
        var bare = game.RateAttack(game, fleet, colony.Planet);

        Assert.NotNull(defended);
        Assert.NotNull(bare);
        Assert.InRange(bare.Value, 0.9, 1.0);
        Assert.True(defended < bare);
    }

    /// <summary>A fleet of a few of the player's ships, out in open space at a point.</summary>
    private static Fleet InSpace(Game game, Player owner, Star star, int x, int y)
    {
        var fleet = Gather(game, owner, star, star.Planets[0], 3);
        fleet.LeaveOrbit();
        fleet.MoveTo(x, y);
        return fleet;
    }

    [Theory]
    [InlineData(true, 30, true)]
    [InlineData(true, 400, false)]
    [InlineData(false, 30, false)]
    public void FleetsAtWarThatComeCloseFightOnTheirOwn(bool war, int apart, bool fight)
    {
        var (game, human, computer) = NewGame(war);
        var star = game.Universe.Stars[human.Colonies[0].Star];
        var ours = InSpace(game, human, star, 1000, 1000);
        var theirs = InSpace(game, computer, star, 1000 + apart, 1000);

        game.Step();

        Assert.Equal(fight, game.Fights(star).Any(f => f.Fleet == ours || f.Fleet == theirs));
    }

    [Fact]
    public void FleetsAtWarCirclingTheSameEmptyPlanetFight()
    {
        var (game, human, computer) = NewGame();
        var star = game.Universe.Stars.First(s => s.Planets.Any(p => p.Colony is null));
        var empty = star.Planets.First(p => p.Colony is null);
        var ours = Gather(game, human, star, empty, 2);
        var theirs = Gather(game, computer, star, empty, 2);
        theirs.EnterOrbit(game.Random, empty);

        game.Step();

        Assert.Contains(game.Fights(star), f => f.Fleet == ours || f.Fleet == theirs);
    }

    [Fact]
    public void AtAColonyOnlyAnAttackStartsAFight()
    {
        var (game, human, computer) = NewGame();
        var colony = computer.Colonies[0];
        var star = game.Universe.Stars[colony.Star];
        var ours = Gather(game, human, star, colony.Planet, 2);
        Assert.Equal(colony.Planet, ours.OrbitCenter);

        for (var i = 0; i < 10; i++)
        {
            game.Step();
        }

        Assert.Empty(game.Fights(star));

        // A computer player's fleet sitting on its colony is at it too: one of ours passing close by does not start a fight.
        var passing = InSpace(game, human, star, colony.Planet.X + 20, colony.Planet.Y);
        game.Step();
        Assert.DoesNotContain(game.Fights(star), f => f.Fleet == passing);
    }

    [Fact]
    public void TheGameRecordsTheMod()
    {
        var (game, _, _) = NewGame();
        Assert.Equal(["simple-combat@1.0"], game.Mods);
    }

    [Fact]
    public void AFleetAttackOpensNoBattleAndIsFoughtOverTime()
    {
        var (game, human, computer) = NewGame();
        var star = game.Universe.Stars[human.Colonies[0].Star];
        var planet = human.Colonies[0].Planet;
        var attacker = Gather(game, human, star, planet, 3);
        var defender = Gather(game, computer, star, planet, 3);
        var before = attacker.Ships.Sum(s => s.Structure) + defender.Ships.Sum(s => s.Structure);
        Assert.True(attacker.Ships.Count > 0 && defender.Ships.Count > 0);

        game.StartFleetBattle(attacker, defender);
        Assert.False(attacker.InCombat);
        Assert.False(defender.InCombat);
        Assert.True(game.ModState.ContainsKey("simple-combat.engaged"));

        for (var i = 0; i < 20; i++)
        {
            game.Step();
        }

        Assert.True(attacker.Ships.Sum(s => s.Structure) + defender.Ships.Sum(s => s.Structure) < before);
    }

    [Fact]
    public void AFleetBesiegesAColonyUntilItFalls()
    {
        var (game, human, computer) = NewGame();
        var colony = computer.Colonies[0];
        var star = game.Universe.Stars[colony.Star];
        var fleet = Gather(game, human, star, colony.Planet, 20);
        Assert.True(fleet.Ships.Count > 0);

        // No defense structures or fleets, so the test is about the people falling and not the odds against the defenses.
        colony.Structures.RemoveAll(s => s.Tech.Kind is >= 7 and <= 10);
        SendAway(computer, star);
        Assert.True(fleet.Ships.Sum(s => game.ShipAttack(s)) > 0, $"attack {fleet.Ships.Count}");
        var events = StepEvents.None;
        game.StartPlanetBattle(fleet, colony.Planet, ref events);
        Assert.False(fleet.InCombat);
        Assert.Equal(colony.Planet, fleet.OrbitCenter);

        for (var i = 0; i < 20000 && colony.Planet.Colony?.Owner == computer && fleet.Ships.Count > 0; i++)
        {
            game.Step();
        }

        Assert.True(colony.Planet.Colony?.Owner == human, $"ships {fleet.Ships.Count} orbit {fleet.OrbitCenter == colony.Planet} pop {colony.CurrentPopulation} def {Game.DefenseHitPoints(colony)} state {string.Join(" | ", game.ModState)} tick {game.Tick} atk {fleet.Ships.Sum(s => game.ShipAttack(s))}");
        Assert.Equal(human, colony.Planet.Colony?.Owner);
    }

    [Fact]
    public void AnAttackMeansWarAndShowsAsAFight()
    {
        var (game, human, computer) = NewGame(war: false);
        var colony = computer.Colonies[0];
        var star = game.Universe.Stars[colony.Star];
        var fleet = Gather(game, human, star, colony.Planet, 5);
        Assert.False(Game.AtWar(human, computer));

        var events = StepEvents.None;
        game.StartPlanetBattle(fleet, colony.Planet, ref events);

        Assert.True(Game.AtWar(human, computer));
        Assert.True(Game.AtWar(computer, human));
        Assert.Contains(new ModFight(fleet, colony.Planet), game.Fights(star));
        Assert.Empty(game.Fights(game.Universe.Stars.First(s => s != star)));
    }

    /// <summary>What a computer fleet loses in 10 steps to the human's missile bases, under the given settings.</summary>
    private double MissileBaseDamage(string? settings)
    {
        var (game, human, computer) = NewGame(settings: settings);
        var colony = human.Colonies[0];
        foreach (var t in game.Tables.Techs.Where(t => t.Kind is TechInfo.MissileBaseKind or TechInfo.MissileKind).Take(4))
        {
            game.TeachTech(human, t);
        }

        game.AddStructure(colony, game.Tables.Techs.First(t => t.Kind == TechInfo.MissileBaseKind));
        var bases = colony.Structures.First(s => s.Tech.Kind == TechInfo.MissileBaseKind);
        (bases.UnderWork, bases.HitPoints) = (false, bases.MaxHitPoints);
        SendAway(human, game.Universe.Stars[colony.Star]);
        var fleet = Gather(game, computer, game.Universe.Stars[colony.Star], colony.Planet, 10);
        var before = fleet.Ships.Sum(s => s.Structure);
        var events = StepEvents.None;
        game.StartPlanetBattle(fleet, colony.Planet, ref events);
        for (var i = 0; i < 10; i++)
        {
            game.Step();
        }

        return before - fleet.Ships.Sum(s => s.Structure);
    }

    [Fact]
    public void MissileBasesFireAsHardAndFastAsTheOriginals()
    {
        // The original's orbitals: shots at half again a ship's power, and a
        // volley then half the reload, where a ship waits all of it. Counted
        // like ships' weapons, a lone missile base barely scratched a fleet.
        var flat = MissileBaseDamage("orbital_power = 1.0\norbital_reload_share = 1.0\n");
        var original = MissileBaseDamage(null);
        Assert.True(flat > 0);
        Assert.True(original >= flat * 2.5, $"{original} against {flat} counted like ships");
    }

    [Fact]
    public void TheDefensesFallFirstAndHitBack()
    {
        var (game, human, computer) = NewGame();
        var colony = computer.Colonies[0];
        SendAway(computer, game.Universe.Stars[colony.Star]); // its fleets would take the first blows
        var fleet = Gather(game, human, game.Universe.Stars[colony.Star], colony.Planet, 20);
        var defenses = Game.DefenseHitPoints(colony);
        var structure = fleet.Ships.Sum(s => s.Structure);
        var population = colony.CurrentPopulation;
        Assert.True(defenses > 0);

        var events = StepEvents.None;
        game.StartPlanetBattle(fleet, colony.Planet, ref events);
        for (var i = 0; i < 10; i++)
        {
            game.Step();
        }

        Assert.True(Game.DefenseHitPoints(colony) < defenses);
        Assert.All(fleet.Ships, s => Assert.True(s.ExperiencePoints > 0)); // a round fought is experience
        Assert.True(fleet.Ships.Sum(s => s.Structure) < structure);
        Assert.Equal(population, colony.CurrentPopulation, 0.5); // the people wait until the defenses are gone
    }

    [Fact]
    public void ARunOfTheModIsTheSameEachTime()
    {
        ulong Run()
        {
            var (game, human, computer) = NewGame();
            var colony = computer.Colonies[0];
            var fleet = Gather(game, human, game.Universe.Stars[colony.Star], colony.Planet, 10);
            var events = StepEvents.None;
            game.StartPlanetBattle(fleet, colony.Planet, ref events);
            for (var i = 0; i < 500; i++)
            {
                game.Step();
            }

            return game.Hash();
        }

        Assert.Equal(Run(), Run());
    }
}
