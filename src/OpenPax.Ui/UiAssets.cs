using OpenPax.Audio;
using OpenPax.Data;
using OpenPax.Data.Art;
using OpenPax.Data.Import;
using OpenPax.Data.Resources;
using OpenPax.Data.Tables;
using OpenPax.Data.Tables.Tech;
using OpenPax.Render;

namespace OpenPax.Ui;

/// <summary>Everything the screens draw with, loaded from the converted data folder.</summary>
public sealed class UiAssets
{
    private readonly Dictionary<int, Palette> _paletteCache = [];
    private readonly Dictionary<ImageSet, PictureLibrary> _pictures = [];
    private readonly string _gameData;
    private readonly string _techData;
    private Dictionary<int, TechRecord>? _techs;

    public UiAssets(string dataFolder)
        : this(new DataFiles(dataFolder))
    {
    }

    /// <summary>The assets from the game's data with mods' data packs over it (docs/modding.md).</summary>
    public UiAssets(DataFiles files)
    {
        Files = files;
        Fonts = new FontLibrary(name => files.Path("fonts", name));
        Strings = new StringTable(ResourceFile.Load(files.Path("original", "Data", "StrData")));
        Views = ResourceFile.Load(files.Path("original", "Data", "ViewData"));
        _gameData = files.Path("original", "Data", "GameData");
        _techData = files.Path("original", "Data", "TechData");

        // Mods' content (docs/modding/content.md): its techs' and hulls' names over the game's strings.
        // Its errors are told once, by the game tables that read the same files.
        Content = OpenPax.Data.Content.ContentPack.Load(files, []);
        foreach (var tech in Content.Techs)
        {
            var list = tech.Record.Base.StringListId;
            var like = 100000 + tech.Look;
            Strings.Override(list, 0, tech.Name ?? Strings.Get(new OpenPax.Data.Views.TextRef(like, 0)));
            Strings.Override(list, 1, tech.Description ?? Strings.Get(new OpenPax.Data.Views.TextRef(like, 1)));
        }

        foreach (var hull in Content.Hulls.Where(h => h.Name is not null))
        {
            Strings.Override(HullClassNames, hull.Type, hull.Name!); // just tested
        }
    }

    // STR# 20005: the class name of each hull type.
    private const int HullClassNames = 20005;

    /// <summary>What mods' data packs add to the game's tables (docs/modding/content.md); empty without them.</summary>
    public OpenPax.Data.Content.ContentPack Content { get; }

    /// <summary>The data files, packs over the game's own.</summary>
    public DataFiles Files { get; }

    /// <summary>Main.img, which holds window backgrounds and buttons.</summary>
    public PictureLibrary MainPictures => Pictures(ImageSet.Main);

    public FontLibrary Fonts { get; }

    /// <summary>Sound and music; silent until the host gives a player.</summary>
    public ISoundPlayer Sound { get; set; } = SilentSound.Instance;

    /// <summary>The game's pointer; one for the app, as the original keeps one.</summary>
    public MouseCursor Cursor { get; } = new();

    /// <summary>Whether pictures come with their 2x versions (art2x/), for the upscaled screens; changing it reloads them.</summary>
    public bool Upscaled
    {
        get;
        set
        {
            if (field != value)
            {
                field = value;
                _pictures.Clear();
            }
        }
    }

    /// <summary>The pictures of one image file, converted to art/name/.</summary>
    public PictureLibrary Pictures(ImageSet set)
    {
        if (!_pictures.TryGetValue(set, out var library))
        {
            var name = set.ToString().ToLowerInvariant();
            // A 2x picture counts only from the pack that gave the 1x one or a
            // higher one, so a pack's own picture is never covered by the game's 2x art.
            var files = Files;
            library = new PictureLibrary(
                id => files.RootIndex("art", name, id + ".png") >= 0 ? files.Path("art", name, id + ".png") : null,
                Upscaled
                    ? id =>
                    {
                        var (low, high) = (files.RootIndex("art", name, id + ".png"), files.RootIndex("art2x", name, id + ".png"));
                        return high >= 0 && high <= low ? files.Path("art2x", name, id + ".png") : null;
                    }
            : null);
            _pictures[set] = library;
        }

        return library;
    }

    public StringTable Strings { get; }

    public ResourceFile Views { get; }

    /// <summary>A tech's full record from Data/TechData, as the panels that describe techs read it.</summary>
    public TechRecord? Tech(int id)
    {
        if (_techs is null)
        {
            var file = ResourceFile.Load(_techData);
            _techs = file.Entries(ResourceType.Tech).ToDictionary(e => e.Id, e => TechRecord.Parse(file.Get(ResourceType.Tech, e.Id).Span));
            foreach (var tech in Content.Techs)
            {
                _techs[tech.Id] = tech.Record;
            }
        }

        return _techs.GetValueOrDefault(id);
    }

    /// <summary>The built-in species, the RACE records of Data/GameData in file order.</summary>
    public IReadOnlyList<RaceTemplate> LoadStockSpecies()
    {
        var file = ResourceFile.Load(_gameData);
        var name = Strings.Get(new Data.Views.TextRef(RaceTemplate.DefaultNameString.List, RaceTemplate.DefaultNameString.Index));
        var description = Strings.Get(new Data.Views.TextRef(RaceTemplate.DefaultDescriptionString.List, RaceTemplate.DefaultDescriptionString.Index));
        return file.Entries(ResourceType.Race)
            .Select(e => RaceTemplate.Parse(file.Get(ResourceType.Race, e.Id).Span, name, description))
            .ToList();
    }

    /// <summary>A window palette by index (0 to 15), which is CPLT 1000 + index in Data/PalData.</summary>
    public Palette Palette(int index)
    {
        if (!_paletteCache.TryGetValue(index, out var palette))
        {
            palette = JascPalette.Parse(File.ReadAllText(Files.Path("palettes", (1000 + index) + ".pal")));
            _paletteCache[index] = palette;
        }

        return palette;
    }
}
