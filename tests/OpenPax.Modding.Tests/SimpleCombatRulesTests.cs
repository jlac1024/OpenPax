using OpenPax.Data.Install;
using OpenPax.Data.Tables;
using OpenPax.Host.Headless;
using OpenPax.Sim;
using OpenPax.Sim.Players;
using OpenPax.Sim.Setup;
using OpenPax.TestSupport;
using SimpleCombat;

namespace OpenPax.Modding.Tests;

/// <summary>Simple Combat's round sums (samples/SimpleCombat/Fight.cs): the battle rules it boils down, one at a time.</summary>
public sealed class SimpleCombatRulesTests
{
    private static GameTables? _tables;
    private static readonly Settings _settings = Settings.Default;

    private static GameTables Tables()
    {
        var folder = GameData.RequireFolder();
        return _tables ??= GameTablesLoader.Load(
            Path.Combine(folder, "Data"),
            DirectionTables.FromExecutable(File.ReadAllBytes(Path.Combine(folder, GameInstall.Executable))),
            SpriteRows.Measure(OpenPax.Data.Resources.ResourceFile.Load(Path.Combine(folder, "Image", "Combat.img"))));
    }

    private static Game NewGame() =>
        OpenPax.Sim.Setup.NewGame.Create(new GameSetup(1234, 30, 2, 3, [new HumanPlayerSetup("Commander", 0, 0, Tables().Species[0])]), Tables());

    /// <summary>A fleet of the human's of a few ships on a medium hull with only the parts given.</summary>
    private static Fleet Fleet(Game game, int ships, params (int Kind, int Mode, bool Last)[] parts)
    {
        var human = game.Players[0];
        var colony = human.Colonies[0];
        var design = new Design(game.Tables.Hulls[3]) { Owner = human };
        foreach (var (kind, mode, last) in parts)
        {
            var techs = game.Tables.OfKind(kind).ToList();
            design.Add(last ? techs[^1] : techs[0], mode);
        }

        var fleet = game.CreateFleet(human, game.Universe.Stars[colony.Star], colony.Planet);
        for (var i = 0; i < ships; i++)
        {
            fleet.AddShip(game.NewShip(design), game.Tables.Tech);
        }

        return fleet;
    }

    private static readonly (int, int, bool) _missile = (TechInfo.MissileKind, 0, false);

    [Fact]
    public void EcmTurnsMissilesAside()
    {
        var game = NewGame();
        var shooter = Firepower.Of(game, [Fleet(game, 3, _missile)]);
        var plain = Firepower.Of(game, [Fleet(game, 3)]);
        var jammed = Firepower.Of(game, [Fleet(game, 3, (TechInfo.EcmKind, -1, true))]);

        Assert.True(shooter.Missile > 0);
        Assert.True(jammed.Ecm > 0);
        Assert.True(Fight.Fire(shooter, jammed, 0, _settings).Missile < Fight.Fire(shooter, plain, 0, _settings).Missile);
    }

    [Fact]
    public void PointDefenseAndShortGunsStopMissilesAndDrones()
    {
        var game = NewGame();
        var shooter = Firepower.Of(game, [Fleet(game, 3, _missile, (TechInfo.FighterKind, -1, false))]);
        var bare = Firepower.Of(game, [Fleet(game, 3)]);
        var guarded = Firepower.Of(game, [Fleet(game, 3, (TechInfo.EnergyWeaponKind, 3, false), (TechInfo.EnergyWeaponKind, 3, false))]);

        Assert.True(shooter.Drones > 0);
        Assert.True(guarded.PointDefense > 0);
        var open = Fight.Fire(shooter, bare, bare.SmallCraftDefense(_settings), _settings);
        var stopped = Fight.Fire(shooter, guarded, guarded.SmallCraftDefense(_settings), _settings);
        Assert.True(stopped.Missile < open.Missile);
        Assert.True(stopped.Drones < open.Drones);
        Assert.True(stopped.Missile >= open.Missile * (1 - _settings.MostStopped) - 1e-9); // never all of them
    }

    [Fact]
    public void VeteransAimBetterAndAreHarderToHit()
    {
        var game = NewGame();
        var green = Fleet(game, 1, (TechInfo.EnergyWeaponKind, 0, false));
        var veteran = Fleet(game, 1, (TechInfo.EnergyWeaponKind, 0, false));
        veteran.Ships[0].AddExperience(1600);
        var (g, v) = (Firepower.Of(game, [green]), Firepower.Of(game, [veteran]));

        Assert.True(veteran.Ships[0].ExperienceLevel > green.Ships[0].ExperienceLevel);
        Assert.True(v.BeamAim > g.BeamAim);
        Assert.True(v.HullDefense > g.HullDefense);
        Assert.True(Fight.Fire(v, g, 0, _settings).Beam > Fight.Fire(g, v, 0, _settings).Beam);
    }

    [Fact]
    public void ShipsThatFoughtGainExperience()
    {
        var game = NewGame();
        var fleet = Fleet(game, 2);
        Fight.Experience([fleet], 30, _settings);
        Assert.All(fleet.Ships, s => Assert.Equal(15 + _settings.ExperiencePerRound, s.ExperiencePoints));
    }

    [Fact]
    public void ShieldsCutBeamsButNotMissiles()
    {
        var game = NewGame();
        var shielded = Fleet(game, 1, (TechInfo.ShieldKind, -1, true));
        var plain = Fleet(game, 1);
        var (a, b) = (shielded.Ships[0], plain.Ships[0]);
        var hit = new Volley(30, 0, 0, 0);
        Fight.OnShips(game, [a], hit, game.Players[1]);
        Fight.OnShips(game, [b], hit, game.Players[1]);
        Assert.True(a.MaxStructure - a.Structure < b.MaxStructure - b.Structure);

        // Missiles land in full on both, the hull and the parts together.
        var shieldedBefore = Taken(a);
        var plainBefore = Taken(b);
        Fight.OnShips(game, [a], new Volley(0, 30, 0, 0), game.Players[1]);
        Fight.OnShips(game, [b], new Volley(0, 30, 0, 0), game.Players[1]);
        Assert.Equal(Taken(b) - plainBefore, Taken(a) - shieldedBefore);
    }

    // All the damage a ship has taken: to its hull and to its components.
    private static int Taken(Design ship) =>
        ship.MaxStructure - ship.Structure
        + ship.Weapons.Concat(ship.Specials).Concat(new[] { ship.Shield, ship.Ecm, ship.Targeting }.OfType<Component>()).Sum(c => c.Damage);

    [Fact]
    public void FireIsSharedAndWearsTheShipsParts()
    {
        var game = NewGame();
        var fleet = Fleet(game, 3, _missile);
        var structure = fleet.Ships[0].MaxStructure;
        Fight.OnShips(game, fleet.Ships, new Volley(structure * 0.9, 0, 0, 0), game.Players[1]);

        // Shared: every ship is hurt and none is lost, where weakest first would have nearly killed one.
        Assert.Equal(3, fleet.Ships.Count);
        Assert.All(fleet.Ships, s => Assert.InRange(s.Structure, structure * 0.6, structure - 1));

        // Battered ships lose parts as the original's hits do: the worse off, the more.
        for (var i = 0; i < 6; i++)
        {
            Fight.OnShips(game, fleet.Ships, new Volley(structure * 0.3, 0, 0, 0), game.Players[1]);
        }

        Assert.Contains(fleet.Ships.SelectMany(s => s.Weapons), w => w.Damage > 0);
    }

    [Fact]
    public void FireLeftOverPassesToTheNextShip()
    {
        var game = NewGame();
        var fleet = Fleet(game, 3);
        var structure = fleet.Ships[0].MaxStructure;
        // More than all three can take: each is destroyed, and what is left goes on.
        var left = Fight.OnShips(game, fleet.Ships, new Volley(structure * 4, 0, 0, 7), game.Players[1]);
        Assert.Empty(fleet.Ships);
        Assert.InRange(left.Total, (structure * 0.9) - 1, structure * 1.1);
        Assert.Equal(7, left.Bombs); // bombs pass over ships
    }
}
