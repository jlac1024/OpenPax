using System.Globalization;
using OpenPax.Data;
using OpenPax.Data.Content;
using OpenPax.Data.Install;
using OpenPax.Data.Tables;
using OpenPax.Data.Tables.Tech;
using OpenPax.Host.Headless;
using OpenPax.Sim;
using OpenPax.Sim.Galaxy;
using OpenPax.Sim.Players;
using OpenPax.Sim.Setup;
using OpenPax.TestSupport;

namespace OpenPax.Modding.Tests;

/// <summary>Content packs (docs/modding/content.md): techs, hulls, planet types and designs a mod's data pack adds.</summary>
public sealed class ContentTests : IDisposable
{
    private readonly TempFolder _temp = new();

    public void Dispose() => _temp.Dispose();

    /// <summary>The game's own data files under base/original/Data, and a pack folder holding content/test.toml.</summary>
    private DataFiles Files(string content)
    {
        var data = Path.Combine(GameData.RequireFolder(), "Data");
        foreach (var name in new[] { "GameData", "HullData", "StrData", "TechData" })
        {
            var to = Path.Combine(_temp.Path, "base", "original", "Data", name);
            Directory.CreateDirectory(Path.GetDirectoryName(to)!); // a path in a folder
            File.Copy(Path.Combine(data, name), to, true);
        }

        _temp.Write("pack/content/test.toml", content);
        return new DataFiles(Path.Combine(_temp.Path, "base"), [Path.Combine(_temp.Path, "pack")]);
    }

    private static GameTables Tables(DataFiles files, List<string> errors)
    {
        var folder = GameData.RequireFolder();
        return GameTablesLoader.Load(
            files,
            DirectionTables.FromExecutable(File.ReadAllBytes(Path.Combine(folder, GameInstall.Executable))),
            SpriteRows.Measure(OpenPax.Data.Resources.ResourceFile.Load(Path.Combine(folder, "Image", "Combat.img"))),
            errors);
    }

    /// <summary>The game's own tables, from the same copied data files with no pack over them.</summary>
    private GameTables Vanilla()
    {
        _ = Files("");
        return Tables(new DataFiles(Path.Combine(_temp.Path, "base")), []);
    }

    [Fact]
    public void ANewTechComesAfterTheGamesLikeTheOneItCopiesWithItsChanges()
    {
        var vanilla = Vanilla();
        var beam = vanilla.OfKind(TechInfo.EnergyWeaponKind).First();
        var errors = new List<string>();
        var tables = Tables(Files($"""
            [[tech]]
            key = "lance"
            like = {beam.Id}
            name = "Plasma Lance"
            damage_by_range = [90, 70, 0, 0]
            research_cost = 12345
            prerequisites = [
              {beam.Id},
            ]
            no_such_field = 1

            [[tech]]
            key = "lance2"
            like = "lance"
            accuracy = 30
            prerequisites = ["lance"]
            """), errors);

        var lance = tables.Techs[vanilla.Techs.Count];
        var lance2 = tables.Techs[vanilla.Techs.Count + 1];
        Assert.Equal(vanilla.Techs.Count + 2, tables.Techs.Count);
        Assert.Equal((vanilla.Techs.Count + 1, TechInfo.EnergyWeaponKind, 12345), (lance.Id, lance.Kind, lance.ResearchCost));
        Assert.Equal([90, 70, 0, 0], lance.Damage);
        Assert.Equal([beam.Id, 0, 0, 0], lance.Prerequisites);
        Assert.Equal(beam.Id, lance.LookId);
        Assert.Equal(beam.Id, lance2.LookId); // a copy of a copy looks like the original
        Assert.Equal(lance.Id, lance2.Prerequisites[0]);
        Assert.Equal(30, lance2.WeaponAccuracy);
        Assert.Equal([90, 70, 0, 0], lance2.Damage);
        Assert.Contains(lance, tables.OfKind(TechInfo.EnergyWeaponKind));
        Assert.Equal(tables.OfKind(TechInfo.EnergyWeaponKind).Count() - 2, tables.IndexInKind(lance));
        Assert.Single(errors, e => e.Contains("no_such_field", StringComparison.Ordinal));
    }

    [Fact]
    public void AChangeToAGameTechAndAHullStaysInPlace()
    {
        var vanilla = Vanilla();
        var drive = vanilla.OfKind(TechInfo.StarDriveKind).First();
        var errors = new List<string>();
        var tables = Tables(Files($"""
            [[tech]]
            id = {drive.Id}
            rating = 9

            [[hull]]
            type = 3
            capacity = 2500
            name = "Heavy Cruiser"
            """), errors);

        Assert.Empty(errors);
        Assert.Equal(vanilla.Techs.Count, tables.Techs.Count);
        Assert.Equal(9, tables.Tech(drive.Id).DriveRating);
        Assert.Equal(2500, tables.Hulls[3].Capacity);
        Assert.Equal("Heavy Cruiser", tables.ClassNames[3]);
        Assert.Equal(vanilla.Hulls[2], tables.Hulls[2]);
    }

    [Fact]
    public void WithoutContentTheTablesAndAGalaxyAreTheOriginals()
    {
        var vanilla = Vanilla();
        var same = Tables(Files(""), []);
        Assert.Equal(vanilla.Techs.Select(t => (t.Id, t.Kind, t.ResearchCost, t.LookId)), same.Techs.Select(t => (t.Id, t.Kind, t.ResearchCost, t.LookId)));
        Assert.Empty(same.PlanetKinds);

        var a = NewGame.Create(new GameSetup(77, 30, 3, 3, []), vanilla);
        var b = NewGame.Create(new GameSetup(77, 30, 3, 3, []), same);
        Assert.Equal(a.Hash(), b.Hash());
        Assert.All(a.Universe.Stars.SelectMany(s => s.Planets), p => Assert.Equal(0, p.Kind));
    }

    [Fact]
    public void NewPlanetTypesAreDrawnAlongsideAndBreatheByTheirOwnRule()
    {
        var errors = new List<string>();
        var tables = Tables(Files("""
            [[planet]]
            key = "crystal"
            name = "Crystalline"
            like = 2
            weight = 3
            breathable = "none"
            rating_bonus = 1
            """), errors);
        Assert.Empty(errors);
        var kind = Assert.Single(tables.PlanetKinds);
        Assert.Equal(("Crystalline", 2, Planet.BreathingNone), (kind.Name, kind.Like, kind.Breathing));

        var game = NewGame.Create(new GameSetup(77, 30, 3, 3, []), tables);
        var crystals = game.Universe.Stars.SelectMany(s => s.Planets).Where(p => p.Kind == 1).ToList();
        Assert.NotEmpty(crystals);
        Assert.All(crystals, p => Assert.Equal(2, p.Class));
        Assert.All(crystals, p => Assert.False(Colony.Breathes(game.Players[0].Species, p)));
        Assert.All(game.Players, p => Assert.Equal(0, game.Universe.Stars[p.HomeStar].Planets.Single(x => x.Slot == p.HomePlanetSlot).Kind));
    }

    [Fact]
    public void APremadeDesignComesOnceItsPartsAreKnown()
    {
        var vanilla = Vanilla();
        var human = NewGame.Create(new GameSetup(77, 30, 2, 3, [new HumanPlayerSetup("C", 0, 0, vanilla.Species[0])]), vanilla).Players[0];
        var known = human.KnownTechs.Select(id => vanilla.Tech(id)).First(t => t.Kind == TechInfo.EnergyWeaponKind || t.Kind == TechInfo.ProjectileKind);
        var later = vanilla.OfKind(TechInfo.ShieldKind).Last();
        var tables = Tables(Files($"""
            [[design]]
            name = "Picket"
            hull = 1
            parts = [[{known.Id}, 0, 2]]

            [[design]]
            name = "Bulwark"
            hull = 3
            parts = [[{known.Id}, 2], [{later.Id}, 1]]
            """), []);

        var game = NewGame.Create(new GameSetup(77, 30, 2, 3, [new HumanPlayerSetup("C", 0, 0, tables.Species[0])]), tables);
        var player = game.Players[0];
        var picket = Assert.Single(player.Designs.OfType<Design>(), d => d.Name == "Picket");
        Assert.Equal(1, picket.Type);
        Assert.Equal(2, picket.Weapons.Sum(w => w.Count));
        Assert.DoesNotContain(player.Designs.OfType<Design>(), d => d.Name == "Bulwark");
        Assert.DoesNotContain(game.Players[1].Designs.OfType<Design>(), d => d.Name == "Picket"); // computer players design their own

        game.TeachTech(player, later);
        Assert.Single(player.Designs.OfType<Design>(), d => d.Name == "Bulwark");
        game.TeachTech(player, game.Tables.OfKind(TechInfo.ArmorKind).Last());
        Assert.Single(player.Designs.OfType<Design>(), d => d.Name == "Bulwark"); // not given twice
    }

    [Fact]
    public void TheArsenalSampleLoadsWhole()
    {
        var repo = new DirectoryInfo(AppContext.BaseDirectory);
        while (!File.Exists(Path.Combine(repo.FullName, "OpenPax.slnx")))
        {
            repo = repo.Parent!; // the tests run inside the repository
        }

        _ = Files("");
        var errors = new List<string>();
        var vanilla = Vanilla();
        var tables = Tables(new DataFiles(Path.Combine(_temp.Path, "base"), [Path.Combine(repo.FullName, "samples", "Arsenal", "data")]), errors);

        Assert.Empty(errors);
        Assert.Equal(vanilla.Techs.Count + 3, tables.Techs.Count);
        Assert.Equal(51, tables.OfKind(TechInfo.ImprovementKind).Count()); // a 51st improvement: colonies make room for it
        Assert.Single(tables.PlanetKinds);
        var lancer = Assert.Single(tables.Designs);
        Assert.Equal([(vanilla.Techs.Count + 1, 0, 2), (vanilla.Techs.Count + 2, -1, 1)], lancer.Parts);

        var game = NewGame.Create(new GameSetup(5, 30, 2, 3, [new HumanPlayerSetup("C", 0, 0, tables.Species[0])]), tables);
        Assert.Equal(51, game.Players[0].Colonies[0].Improvements.Length);
        game.TeachTech(game.Players[0], tables.Techs[^1]);
        Assert.NotNull(game.Players[0].Colonies[0].Improvements[50]);
    }

    [Fact]
    public void AContentFileReadsListsOverSeveralLines()
    {
        var t = OpenPax.Data.Toml.TomlReader.Parse("a = [\n  1, # one\n  [2, 3],\n]\nb = \"x [y\"\n");
        Assert.Equal([1L, new List<object> { 2L, 3L }], (List<object>)t["a"]);
        Assert.Equal("x [y", t["b"]);
    }

    [Fact]
    public void FieldsAreNamedInSnakeCase()
    {
        Assert.Equal("damage_by_range", RecordFields.Snake("DamageByRange"));
        Assert.Contains(RecordFields.Of(typeof(ShipWeapon)), f => f.Name == "reload" && f.Type == typeof(int));
        Assert.Equal(typeof(ShipWeapon), ContentPack.DetailsOf(6005));
        _ = CultureInfo.InvariantCulture;
    }
}
