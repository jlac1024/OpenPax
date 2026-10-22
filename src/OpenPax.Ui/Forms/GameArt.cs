using OpenPax.Data;
using OpenPax.Data.Png;
using OpenPax.Forms.Drawing;
using OpenPax.Render.TrueColor;

namespace OpenPax.Ui.Forms;

/// <summary>
/// The game's art for forms by name, "Set:Id" as "Main:44300", or
/// "Set@2x:Id" for its upscaled version: the converted PNG in its own
/// palette, so each picture has the colors of the screen it was made for.
/// Read once and kept.
/// </summary>
public sealed class GameArt(DataFiles files) : IArtSource
{
    private readonly Dictionary<string, RgbaImage?> _cache = new(StringComparer.OrdinalIgnoreCase);

    public RgbaImage? Get(string name)
    {
        if (_cache.TryGetValue(name, out var known))
        {
            return known;
        }

        var parts = name.Split(':');
        RgbaImage? image = null;
        var upscaled = parts.Length == 2 && parts[0].EndsWith("@2x", StringComparison.OrdinalIgnoreCase);
        var set = upscaled ? parts[0][..^3] : parts.Length == 2 ? parts[0] : "";
        if (parts.Length == 2 && set.Length > 0 && set.All(char.IsLetter) && parts[1].All(char.IsDigit))
        {
            // Through the data packs (docs/modding.md): a pack's own picture is never covered by the game's 2x one.
            var (low, high) = (files.RootIndex("art", set.ToLowerInvariant(), parts[1] + ".png"), files.RootIndex("art2x", set.ToLowerInvariant(), parts[1] + ".png"));
            var path = upscaled && !(high >= 0 && high <= low) ? "" : files.Path(upscaled ? "art2x" : "art", set.ToLowerInvariant(), parts[1] + ".png");
            image = File.Exists(path) ? RgbaImage.From(PngReader.Read(File.ReadAllBytes(path))) : null;
        }

        _cache[name] = image;
        return image;
    }

    /// <summary>A picture that must be there for the theme.</summary>
    public RgbaImage Need(string name) => Get(name) ?? throw new FileNotFoundException(name);
}
