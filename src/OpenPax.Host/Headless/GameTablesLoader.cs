using OpenPax.Data;
using OpenPax.Data.Content;
using OpenPax.Data.Resources;
using OpenPax.Data.Tables;
using OpenPax.Data.Tables.Tech;
using OpenPax.Data.Text;
using OpenPax.Sim.Galaxy;
using OpenPax.Sim.Players;
using OpenPax.Sim.Setup;

namespace OpenPax.Host.Headless;

/// <summary>
/// Builds the simulation's game tables from the original data files
/// (Data/ in the game folder or data/original/Data) and the direction
/// tables. Also compiled into the simulation tests.
/// </summary>
internal static class GameTablesLoader
{
    private const int HullTypes = 6;

    public static GameTables Load(string dataFolder, DirectionTables directions, CombatSprites combatSprites) =>
        Load(name => Path.Combine(dataFolder, name), directions, combatSprites);

    /// <summary>
    /// The tables with data packs over the game's (docs/modding.md): the
    /// game's own Data files, or a pack's in their place, and the packs'
    /// content (docs/modding/content.md). What the content gets wrong is
    /// told in the errors, and left out.
    /// </summary>
    public static GameTables Load(DataFiles files, DirectionTables directions, CombatSprites combatSprites, ICollection<string>? errors = null) =>
        Load(name => files.Path("original", "Data", name), directions, combatSprites, files, errors ?? []);

    private static GameTables Load(Func<string, string> file, DirectionTables directions, CombatSprites combatSprites, DataFiles? files = null, ICollection<string>? errors = null)
    {
        var strings = ResourceFile.Load(file("StrData"));
        IReadOnlyList<string> List(int id) => StringList.Parse(strings.Get(ResourceType.StringList, id).Span);

        var gameData = ResourceFile.Load(file("GameData"));
        var species = gameData.Entries(ResourceType.Race)
            .Select(e => RaceTemplate.Parse(gameData.Get(ResourceType.Race, e.Id).Span, List(100)[3], List(85)[0]))
            .Select(ToSpecies)
            .ToList();

        var techFile = ResourceFile.Load(file("TechData"));
        var techRecords = techFile.Entries(ResourceType.Tech)
            .Select(e => TechRecord.Parse(techFile.Get(ResourceType.Tech, e.Id).Span))
            .ToList();

        var hullFile = ResourceFile.Load(file("HullData"));
        var hullRecords = Enumerable.Range(0, HullTypes)
            .Select(t => Hull.Parse(Hull.FirstId + t, hullFile.Get(ResourceType.Hull, Hull.FirstId + t).Span))
            .ToList();
        var classNames = List(20005).ToList();

        // Mods' content: techs changed in place, new ones after, hulls changed, new planet types and designs.
        var content = files is null ? ContentPack.Empty : ContentPack.Load(files, techRecords, hullRecords, errors ?? []);
        var looks = new Dictionary<int, int>();
        foreach (var t in content.Techs)
        {
            if (t.Id <= techRecords.Count)
            {
                techRecords[t.Id - 1] = t.Record;
            }
            else
            {
                techRecords.Add(t.Record);
            }

            looks[t.Id] = t.Look;
        }

        foreach (var h in content.Hulls)
        {
            hullRecords[h.Type] = h.Record;
            if (h.Name is not null && h.Type < classNames.Count)
            {
                classNames[h.Type] = h.Name;
            }
        }

        var techs = techRecords.Select(r => ToTech(r) with { Look = looks.TryGetValue(r.Base.TechId, out var look) && look != r.Base.TechId ? look : 0 }).ToList();
        var hulls = hullRecords.Select((h, t) => new HullInfo(t, h.Structure, h.Capacity, h.Cost)).ToList();

        return new GameTables(
            species, List(46000), new NameTables(List(1000), List(111), List(110)[2]), List(108)[10], techs,
            hulls, List(26000), classNames, directions.Sin, directions.Cos, combatSprites.Rows, List(112)[6], List(82)[7], List(82)[6], List(100)[2])
        {
            CombatPictureSizes = combatSprites.Sizes,
            PlanetKinds = [.. content.Planets.Select(p => new PlanetKind(p.Key, p.Name, p.Like, p.Weight, Breathing(p.Breathable), p.RatingBonus))],
            Designs = [.. content.Designs.Select(d => new DesignTemplate(d.Name, d.Hull, d.Quality, d.Parts))],
        };
    }

    private static int Breathing(string rule) => rule switch
    {
        "all" => Planet.BreathingAll,
        "none" => Planet.BreathingNone,
        "anaerobic" => Planet.BreathingAnaerobic,
        _ => Planet.BreathingLike,
    };

    public static Species ToSpecies(RaceTemplate r)
    {
        return new Species(r.RaceId, r.Name, r.Picture, r.PreferredTemperature, r.TemperatureTolerance,
            r.Atmospheres, r.PhysicalTraits, r.CulturalTraits, r.RateModifiers, r.ResearchLevels);
    }

    private static TechInfo ToTech(TechRecord record)
    {
        var b = record.Base;
        var tech = new TechInfo(b.TechId, b.Kind, b.ResearchCost)
        {
            ClassId = record.ClassId,
            Picture = b.Picture,
            ResearchGroup = b.ResearchGroup,
            Cost = b.Cost,
            BuildCost = b.BuildCost,
            Prerequisites = b.Prerequisites,
            HitPoints = b.HitPoints,
            Size = b.Size,
        };
        return record.Details switch
        {
            StarDrive d => tech with { DriveRating = d.Rating, MaxHullClass = d.MaxHullClass },
            TacticalDrive d => tech with { CombatSpeed = d.CombatSpeed, DefenseBonus = d.DefenseBonus },
            ShipWeapon w => tech with { WeaponEffect = w.Effect, Damage = [.. w.DamageByRange.Select(x => (int)x)], Reload = w.Reload, WeaponType = w.WeaponType, WeaponFlag = w.Tracking, WeaponAccuracy = w.Accuracy, MissileHitPoints = w.Secondary, ShotSpeed = w.Unknown5C * 2 },
            Shield s => tech with { ShieldCapacity = s.Capacity, ShieldPercent = s.Percentereduction, ShieldRegeneration = s.Regeneration },
            TargetingSensor t => tech with { SensorAccuracy = t.AccuracyBonus, SensorRange = t.Range, SensorTargets = t.TargetsTracked },
            Ecm e => tech with { EcmStrength = e.Strength },
            Armor a => tech with { StructureMultiplier = a.StructureMultiplier, ArmorHullLimit = a.MaxHullClass },
            Installation i => tech with { InstallationLevel = i.Level, InstallationStrength = i.Strength },
            PlanetImprovement p => tech with { PerPopulation = p.PerPopulation, Flat = p.Flat },
            Fighter f => tech with { FighterWeapon1 = f.Weapon1, FighterWeapon2 = f.Weapon2, FighterDefense = f.Defense, FighterHitPoints = f.HitPoints, FighterSpeed = f.Speed, FighterAccuracy = f.Accuracy, FighterMissileDefense = f.MissileDefense },
            _ => tech,
        };
    }
}
