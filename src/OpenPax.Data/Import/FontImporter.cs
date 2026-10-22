using System.Globalization;
using System.Text;
using OpenPax.Data.Art;
using OpenPax.Data.Png;
using OpenPax.Data.Text;

namespace OpenPax.Data.Import;

/// <summary>
/// Converts each <c>Data/pft*</c> bitmap font into PNG glyph sheets (one per
/// glyph set) and a TOML file with the metrics needed to draw from them.
/// See docs/formats/fonts.md.
/// </summary>
public static class FontImporter
{
    public const string FontFolder = "fonts";
    private const int Columns = 16;

    // Index 0 is background (transparent), index 1 is ink. Text color is
    // applied when drawing, so the sheet only needs on and off.
    private static readonly Rgb[] _palette = [new(0, 0, 0), new(255, 255, 255)];

    public static int ImportAll(string gameFolder, string outFolder)
    {
        var dir = Directory.CreateDirectory(Path.Combine(outFolder, FontFolder)).FullName;
        var count = 0;
        foreach (var path in Directory.GetFiles(Path.Combine(gameFolder, "Data"), "pft*").Order(StringComparer.Ordinal))
        {
            var name = Path.GetFileName(path);
            var font = BitmapFont.Parse(File.ReadAllBytes(path));
            var toml = new StringBuilder();
            toml.Append(CultureInfo.InvariantCulture, $"# Glyph sheets converted from Data/{name}.\n\n");
            WriteSet(font.Regular, "regular", name, dir, toml);
            WriteSet(font.Bold, "bold", name, dir, toml);
            File.WriteAllText(Path.Combine(dir, name + ".toml"), toml.ToString());
            count++;
        }

        return count;
    }

    private static void WriteSet(GlyphSet? set, string label, string name, string dir, StringBuilder toml)
    {
        if (set is null)
        {
            return;
        }

        var cellWidth = Math.Max(1, set.Glyphs.Max(g => g.Width)) + 1;
        var cellHeight = Math.Max(1, set.Glyphs.Max(g => g.Rows));
        var rows = (set.Glyphs.Count + Columns - 1) / Columns;
        var width = cellWidth * Columns;
        var height = cellHeight * rows;
        var pixels = new byte[width * height];
        var sheet = $"{name}-{label}.png";

        toml.Append(CultureInfo.InvariantCulture, $"[{label}]\n");
        toml.Append(CultureInfo.InvariantCulture, $"sheet = \"{sheet}\"\nfirst = {set.First}\nlast = {set.Last}\n");
        toml.Append("metrics = [").Append(string.Join(", ", set.Metrics)).Append("]\n\n");
        for (var i = 0; i < set.Glyphs.Count; i++)
        {
            var g = set.Glyphs[i];
            var x0 = i % Columns * cellWidth;
            var y0 = i / Columns * cellHeight;
            for (var y = 0; y < g.Rows; y++)
            {
                for (var x = 0; x < g.Width; x++)
                {
                    if (g.IsSet(x, y))
                    {
                        pixels[(y0 + y) * width + x0 + x] = 1;
                    }
                }
            }

            toml.Append(CultureInfo.InvariantCulture, $"[[{label}.glyph]]\ncode = {g.Code}\nx = {x0}\ny = {y0}\nwidth = {g.Width}\nrows = {g.Rows}\n\n");
        }

        using var png = File.Create(Path.Combine(dir, sheet));
        PngWriter.Write(png, new IndexedImage(width, height, pixels, _palette, [0, 255]));
    }
}
