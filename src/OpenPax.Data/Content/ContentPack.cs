using System.Globalization;
using OpenPax.Data.Tables;
using OpenPax.Data.Tables.Tech;
using OpenPax.Data.Toml;

namespace OpenPax.Data.Content;

/// <summary>
/// What data packs add to the game's tables (docs/modding/content.md): the
/// TOML files in each pack's content/ folder, read from the pack lowest in
/// priority up, so a later mod can build on an earlier one's entries. New
/// techs and planet types take the next free numbers in that order, so the
/// same mod set always numbers them the same. An entry that does not read is
/// reported and left out; the rest stand.
/// </summary>
public sealed class ContentPack
{
    public const string Folder = "content";

    // The original's tech classes by the names content files use.
    private static readonly Dictionary<string, int> _classes = new(StringComparer.Ordinal)
    {
        ["star_drive"] = 6001,
        ["tactical_drive"] = 6002,
        ["sensor"] = 6003,
        ["ecm"] = 6004,
        ["weapon"] = 6005,
        ["shield"] = 6006,
        ["fighter"] = 6007,
        ["armor"] = 6008,
        ["special"] = 6009,
        ["improvement"] = 6010,
    };

    private const int OriginalPlanetClasses = 6;

    private readonly List<TechContent> _techs = [];
    private readonly Dictionary<string, int> _keys = new(StringComparer.Ordinal);
    private readonly List<HullContent> _hulls = [];
    private readonly List<PlanetContent> _planets = [];
    private readonly List<DesignContent> _designs = [];

    private ContentPack()
    {
    }

    public static ContentPack Empty { get; } = new();

    /// <summary>Changed and new techs, in the order read; a tech changed twice is here twice, the later winning.</summary>
    public IReadOnlyList<TechContent> Techs => _techs;

    public IReadOnlyList<HullContent> Hulls => _hulls;

    public IReadOnlyList<PlanetContent> Planets => _planets;

    public IReadOnlyList<DesignContent> Designs => _designs;

    /// <summary>The class names content files use for the original's tech classes.</summary>
    public static IReadOnlyDictionary<string, int> Classes => _classes;

    /// <summary>The detail record of each tech class, as a content file's fields for it come from.</summary>
    public static Type DetailsOf(int classId) => Default(classId).GetType();

    /// <summary>The packs' content over the game's own techs and hulls, read from the same data files (TechData and HullData, a pack's in their place).</summary>
    public static ContentPack Load(DataFiles files, ICollection<string> errors)
    {
        if (files.Overlays.Count == 0)
        {
            return Empty;
        }

        var techFile = Resources.ResourceFile.Load(files.Path("original", "Data", "TechData"));
        var techs = techFile.Entries(Resources.ResourceType.Tech).Select(e => TechRecord.Parse(techFile.Get(Resources.ResourceType.Tech, e.Id).Span)).ToList();
        var hullFile = Resources.ResourceFile.Load(files.Path("original", "Data", "HullData"));
        var hulls = Enumerable.Range(0, 6).Select(t => Hull.Parse(Hull.FirstId + t, hullFile.Get(Resources.ResourceType.Hull, Hull.FirstId + t).Span)).ToList();
        return Load(files, techs, hulls, errors);
    }

    public static ContentPack Load(DataFiles files, IReadOnlyList<TechRecord> originals, IReadOnlyList<Hull> hulls, ICollection<string> errors)
    {
        var pack = new ContentPack();
        foreach (var root in files.Overlays.Reverse())
        {
            var folder = Path.Combine(root, Folder);
            if (!Directory.Exists(folder))
            {
                continue;
            }

            foreach (var path in Directory.GetFiles(folder, "*.toml").Order(StringComparer.Ordinal))
            {
                var where = Path.GetRelativePath(Path.GetDirectoryName(root) ?? root, path);
                try
                {
                    pack.Read(TomlReader.Parse(File.ReadAllText(path)), where, originals, hulls, errors);
                }
                catch (Exception e) when (e is FormatException or IOException or UnauthorizedAccessException)
                {
                    errors.Add(where + ": " + e.Message);
                }
            }
        }

        return pack;
    }

    /// <summary>The newest record of a tech: as content left it, else the original.</summary>
    private TechRecord? Current(int id, IReadOnlyList<TechRecord> originals) =>
        _techs.LastOrDefault(t => t.Id == id)?.Record ?? (id >= 1 && id <= originals.Count ? originals[id - 1] : null);

    private void Read(Dictionary<string, object> file, string where, IReadOnlyList<TechRecord> originals, IReadOnlyList<Hull> hulls, ICollection<string> errors)
    {
        void Each(string table, Action<Dictionary<string, object>, string> read)
        {
            var n = 0;
            foreach (var entry in TomlReader.Tables(file, table))
            {
                n++;
                var label = string.Format(CultureInfo.InvariantCulture, "{0}: {1} {2}", where, table, entry.TryGetValue("key", out var k) ? k : entry.TryGetValue("id", out var i) ? i : n);
                try
                {
                    read(entry, label);
                }
                catch (Exception e) when (e is FormatException or InvalidCastException or KeyNotFoundException)
                {
                    errors.Add(label + ": " + e.Message);
                }
            }
        }

        Each("tech", (e, label) => ReadTech(e, label, originals, errors));
        Each("hull", (e, label) => ReadHull(e, label, hulls, errors));
        Each("planet", (e, label) => ReadPlanet(e, label, errors));
        Each("design", (e, _) => ReadDesign(e, originals));
    }

    private void ReadTech(Dictionary<string, object> e, string label, IReadOnlyList<TechRecord> originals, ICollection<string> errors)
    {
        var used = new HashSet<string>(StringComparer.Ordinal) { "key", "id", "like", "class", "name", "description" };
        var key = e.TryGetValue("key", out var k) ? (string)k : null;
        int id;
        TechRecord start;
        int look;
        if (key is null)
        {
            // A change to a tech that is already there.
            id = TechId(e.TryGetValue("id", out var given) ? given : throw new FormatException(Strings.ContentNoKey));
            start = Current(id, originals) ?? throw new FormatException(string.Format(CultureInfo.InvariantCulture, Strings.ContentNoTech, id));
            look = _techs.LastOrDefault(t => t.Id == id)?.Look ?? id;
        }
        else
        {
            if (_keys.ContainsKey(key))
            {
                throw new FormatException(string.Format(CultureInfo.InvariantCulture, Strings.ContentKeyTaken, key));
            }

            id = originals.Count + _techs.Count(t => t.Key is not null) + 1;
            if (e.TryGetValue("like", out var like))
            {
                var likeId = TechId(like);
                start = Current(likeId, originals) ?? throw new FormatException(string.Format(CultureInfo.InvariantCulture, Strings.ContentNoTech, like));
                look = _techs.LastOrDefault(t => t.Id == likeId)?.Look ?? likeId;
            }
            else
            {
                var classId = e.TryGetValue("class", out var c) ? ClassId(c) : throw new FormatException(Strings.ContentNoClass);
                start = new TechRecord(classId, new TechBase(0, 0, 0, 0, 0, 0, 0, [0, 0, 0, 0], 0, 0, 0, 0), Default(classId));
                look = id;
            }
        }

        // Tech ids in a list or a field may be written as keys.
        var values = e.ToDictionary(p => p.Key, p => p.Key switch
        {
            "prerequisites" when p.Value is List<object> list => (object)list.Select(x => (object)(long)TechId(x)).ToList(),
            "weapon1" or "weapon2" => (long)TechId(p.Value),
            _ => p.Value,
        }, StringComparer.Ordinal);
        if (values.TryGetValue("prerequisites", out var pre) && pre is List<object> { Count: < 4 } short4)
        {
            while (short4.Count < 4)
            {
                short4.Add(0L);
            }
        }

        var record = start with
        {
            Base = RecordFields.With(start.Base, values, used) with { TechId = id },
            Details = RecordFields.With(start.Details, values, used),
        };
        foreach (var unknown in values.Keys.Where(n => !used.Contains(n)).Order(StringComparer.Ordinal))
        {
            errors.Add(string.Format(CultureInfo.InvariantCulture, Strings.ContentNoField, label, unknown));
        }

        _techs.Add(new TechContent(id, key, record, e.TryGetValue("name", out var name) ? (string)name : null, e.TryGetValue("description", out var d) ? (string)d : null, look));
        if (key is not null)
        {
            _keys[key] = id;
        }
    }

    private void ReadHull(Dictionary<string, object> e, string label, IReadOnlyList<Hull> hulls, ICollection<string> errors)
    {
        var type = e.TryGetValue("type", out var t) ? (int)(long)t : throw new FormatException(Strings.ContentNoHull);
        if (type < 0 || type >= hulls.Count)
        {
            throw new FormatException(Strings.ContentNoHull);
        }

        var used = new HashSet<string>(StringComparer.Ordinal) { "type", "name", "id" };
        var start = _hulls.LastOrDefault(h => h.Type == type)?.Record ?? hulls[type];
        var record = RecordFields.With(start, e.Where(p => p.Key != "id").ToDictionary(p => p.Key, p => p.Value, StringComparer.Ordinal), used);
        foreach (var unknown in e.Keys.Where(n => !used.Contains(n)).Order(StringComparer.Ordinal))
        {
            errors.Add(string.Format(CultureInfo.InvariantCulture, Strings.ContentNoField, label, unknown));
        }

        _hulls.Add(new HullContent(type, record, e.TryGetValue("name", out var n) ? (string)n : _hulls.LastOrDefault(h => h.Type == type)?.Name));
    }

    private void ReadPlanet(Dictionary<string, object> e, string label, ICollection<string> errors)
    {
        var key = e.TryGetValue("key", out var k) ? (string)k : throw new FormatException(Strings.ContentNoKey);
        if (_planets.Any(p => p.Key == key))
        {
            throw new FormatException(string.Format(CultureInfo.InvariantCulture, Strings.ContentKeyTaken, key));
        }

        var like = e.TryGetValue("like", out var l) ? (int)(long)l : 0;
        if (like is < 0 or > 4)
        {
            throw new FormatException(Strings.ContentBadLike);
        }

        var breathable = e.TryGetValue("breathable", out var b) ? (string)b : "like";
        if (breathable is not ("like" or "all" or "none" or "anaerobic"))
        {
            throw new FormatException(Strings.ContentBadBreathable);
        }

        foreach (var unknown in e.Keys.Where(n => n is not ("key" or "name" or "like" or "weight" or "breathable" or "rating_bonus")).Order(StringComparer.Ordinal))
        {
            errors.Add(string.Format(CultureInfo.InvariantCulture, Strings.ContentNoField, label, unknown));
        }

        var weight = e.TryGetValue("weight", out var w) ? w is long wl ? wl : (double)w : 1.0;
        _planets.Add(new PlanetContent(OriginalPlanetClasses + _planets.Count, key, e.TryGetValue("name", out var n) ? (string)n : key, like, Math.Max(0, weight), breathable, e.TryGetValue("rating_bonus", out var r) ? (int)(long)r : 0));
    }

    private void ReadDesign(Dictionary<string, object> e, IReadOnlyList<TechRecord> originals)
    {
        var name = e.TryGetValue("name", out var n) ? (string)n : throw new FormatException(Strings.ContentNoName);
        var hull = e.TryGetValue("hull", out var h) ? (int)(long)h : throw new FormatException(Strings.ContentNoHull);
        var quality = e.TryGetValue("quality", out var q) ? (int)(long)q : 4;
        var parts = new List<(int, int, int)>();
        foreach (var part in e.TryGetValue("parts", out var p) ? (List<object>)p : [])
        {
            // [tech, count] or [tech, mode, count]; a weapon without a mode goes in the bow.
            var fields = (List<object>)part;
            var tech = TechId(fields[0]);
            var record = Current(tech, originals) ?? throw new FormatException(string.Format(CultureInfo.InvariantCulture, Strings.ContentNoTech, fields[0]));
            var (mode, count) = fields.Count >= 3 ? ((int)(long)fields[1], (int)(long)fields[2]) : (record.ClassId == 6005 ? 0 : -1, fields.Count == 2 ? (int)(long)fields[1] : 1);
            parts.Add((tech, mode, count));
        }

        _designs.Add(new DesignContent(name, Math.Clamp(hull, 0, 5), Math.Clamp(quality, 0, 4), parts));
    }

    private int TechId(object value) => value switch
    {
        long l => (int)l,
        string key when _keys.TryGetValue(key, out var id) => id,
        string key => throw new FormatException(string.Format(CultureInfo.InvariantCulture, Strings.ContentNoTech, key)),
        _ => throw new FormatException(Strings.ContentNoKey),
    };

    private static int ClassId(object value) => value switch
    {
        long l when l is >= 6001 and <= 6010 => (int)l,
        string s when _classes.TryGetValue(s, out var id) => id,
        _ => throw new FormatException(Strings.ContentBadClass),
    };

    private static TechDetails Default(int classId) => classId switch
    {
        6001 => new StarDrive(0, 2),
        6002 => new TacticalDrive(0, 0),
        6003 => new TargetingSensor(0, 0, 0),
        6004 => new Ecm(0),
        6005 => new ShipWeapon(0, 0, [0, 0, 0, 0], 0, 10, 0, 10, 0, 0, 255, 0),
        6006 => new Shield(0, 0, 0, 0, 0),
        6007 => new Fighter(0, 0, 0, 0, 0, 0, 0),
        6008 => new Armor(5, 1.0),
        6009 => new Installation(0, 0),
        _ => new PlanetImprovement([0, 0, 0, 0, 0], [0, 0, 0, 0, 0]),
    };
}
