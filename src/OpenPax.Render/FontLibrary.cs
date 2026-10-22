namespace OpenPax.Render;

/// <summary>The game's fonts by the index window layouts use.</summary>
public sealed class FontLibrary
{
    private readonly Func<string, string> _find;
    private readonly Dictionary<int, BitmapText> _loaded = [];

    public FontLibrary(string folder)
        : this(name => Path.Combine(folder, name))
    {
    }

    /// <summary>The fonts by a lookup from a file's path in the fonts folder to where it is (a mod's data pack over the game's).</summary>
    public FontLibrary(Func<string, string> find)
    {
        _find = find;
        Settings = new TextSettings { Modern = Fonts.ModernFont.Load(find) };
    }

    /// <summary>How every font of the library draws its text.</summary>
    public TextSettings Settings { get; }

    /// <summary>Bit 0x80 of the index is ignored; unknown indices get the default font.</summary>
    public BitmapText ForIndex(int index)
    {
        var size = (index & 0x7F) switch
        {
            1 => 10,
            2 => 11,
            3 => 12,
            4 => 14,
            6 => 8,
            _ => 9,
        };
        if (!_loaded.TryGetValue(size, out var font))
        {
            font = BitmapText.Load(_find("pft" + size + ".toml"));
            font.Settings = Settings;
            _loaded[size] = font;
        }

        return font;
    }
}
