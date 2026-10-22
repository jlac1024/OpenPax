namespace OpenPax.Render.Fonts;

/// <summary>
/// The modern text font (Titillium Web, regular and bold): glyphs filled at
/// whole pixel sizes and kept. Sizes are chosen so its capitals stand as
/// tall as the game font's they replace.
/// </summary>
public sealed class ModernFont(TrueTypeFont regularFont, TrueTypeFont boldFont)
{
    public const string Folder = "modern";

    private readonly Dictionary<(char, int, bool), RasterGlyph> _glyphs = [];

    /// <summary>The fonts in data/fonts/modern, or null when they are not there.</summary>
    public static ModernFont? Load(string fontsFolder) => Load(name => Path.Combine(fontsFolder, name));

    /// <summary>The fonts by a lookup from a path in the fonts folder (a mod's data pack may replace them).</summary>
    public static ModernFont? Load(Func<string, string> find)
    {
        var r = find(Path.Combine(Folder, "TitilliumWeb-Regular.ttf"));
        var b = find(Path.Combine(Folder, "TitilliumWeb-Bold.ttf"));
        return File.Exists(r) && File.Exists(b) ? new ModernFont(TrueTypeFont.Load(r), TrueTypeFont.Load(b)) : null;
    }

    /// <summary>The pixel size whose capitals are a given number of pixels tall.</summary>
    public int SizeForCapitals(int pixels) => Math.Max(4, (int)MathF.Round(pixels / 0.69f));

    public RasterGlyph Glyph(char c, int size, bool bold)
    {
        if (_glyphs.TryGetValue((c, size, bold), out var known))
        {
            return known;
        }

        var font = bold ? boldFont : regularFont;
        var id = font.GlyphFor(c);
        var scale = size / (float)font.UnitsPerEm;
        var outline = font.Outline(id);
        RasterGlyph glyph;
        if (outline.Count == 0)
        {
            glyph = new RasterGlyph([], 0, 0, 0, 0, font.Advance(id) * scale);
        }
        else
        {
            var points = outline.SelectMany(p => p).ToList();
            var left = (int)MathF.Floor(points.Min(p => p.X) * scale);
            var right = (int)MathF.Ceiling(points.Max(p => p.X) * scale) + 1;
            var top = (int)MathF.Ceiling(points.Max(p => p.Y) * scale) + 1;
            var bottom = (int)MathF.Floor(points.Min(p => p.Y) * scale);
            var (w, h) = (right - left, top - bottom);
            var pixels = outline.Select(contour => (IReadOnlyList<(float X, float Y)>)[.. contour.Select(p => (p.X * scale - left, top - p.Y * scale))]);
            glyph = new RasterGlyph(GlyphRasterizer.Fill(pixels, w, h), w, h, left, top, font.Advance(id) * scale);
        }

        _glyphs[(c, size, bold)] = glyph;
        return glyph;
    }

    /// <summary>How far the font reaches above its baseline at a size, in pixels (its own ascender, room for accents included).</summary>
    public int Ascent(int size, bool bold) => (int)MathF.Ceiling((bold ? boldFont : regularFont).Ascender * size / (float)(bold ? boldFont : regularFont).UnitsPerEm);

    /// <summary>How far it reaches below, in pixels (its own descender).</summary>
    public int Descent(int size, bool bold) => (int)MathF.Ceiling(-(bold ? boldFont : regularFont).Descender * size / (float)(bold ? boldFont : regularFont).UnitsPerEm);

    /// <summary>The width a line takes at a size.</summary>
    public int Measure(string text, int size, bool bold) => (int)MathF.Ceiling(text.Sum(c => Glyph(c, size, bold).Advance));
}
