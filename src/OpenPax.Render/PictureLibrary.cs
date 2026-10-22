using OpenPax.Data.Png;

namespace OpenPax.Render;

/// <summary>
/// The converted pictures of one image file (data/art/&lt;name&gt;/&lt;id&gt;.png),
/// loaded on first use. The PNG's own palette is ignored: pictures are
/// drawn with the palette of the window that shows them.
/// </summary>
public sealed class PictureLibrary
{
    private readonly Func<int, string?> _find;
    private readonly Func<int, string?>? _findHd;
    private readonly Dictionary<int, Sprite?> _cache = [];

    /// <param name="hdFolder">Where the 2x pictures are (data/art2x/&lt;name&gt;), or null to load none.</param>
    public PictureLibrary(string folder, string? hdFolder = null)
        : this(id => Path.Combine(folder, id + ".png"), hdFolder is null ? null : id => Path.Combine(hdFolder, id + ".png"))
    {
    }

    /// <summary>Pictures found by lookups (a mod's data pack over the game's, docs/modding.md): each gives a picture's file, or null for none.</summary>
    public PictureLibrary(Func<int, string?> find, Func<int, string?>? findHd)
    {
        _find = find;
        _findHd = findHd;
    }

    /// <summary>The picture with this id, or null when the image file has none (the original then draws nothing).</summary>
    public Sprite? Get(int id)
    {
        if (!_cache.TryGetValue(id, out var sprite))
        {
            var path = _find(id);
            if (path is not null && File.Exists(path))
            {
                var image = PngReader.Read(File.ReadAllBytes(path));
                sprite = new Sprite(image.Width, image.Height, image.Pixels) { Hd = LoadHd(id) };
            }

            _cache[id] = sprite;
        }

        return sprite;
    }

    private Sprite? LoadHd(int id)
    {
        var path = _findHd?.Invoke(id);
        if (path is null || !File.Exists(path))
        {
            return null;
        }

        var image = PngReader.Read(File.ReadAllBytes(path));
        return new Sprite(image.Width, image.Height, image.Pixels);
    }
}
