using OpenPax.Data.Png;
using OpenPax.Data.Toml;

namespace OpenPax.Render;

/// <summary>One weight of a bitmap font: the glyph sheet and where each glyph sits in it.</summary>
public sealed class FontFace
{
    private readonly Dictionary<int, GlyphBox> _glyphs;

    private FontFace(IndexedImage sheet, Dictionary<int, GlyphBox> glyphs, int ascent, IReadOnlyList<int> metrics)
    {
        Sheet = sheet;
        _glyphs = glyphs;
        Ascent = ascent;
        Metrics = metrics;
    }

    public IndexedImage Sheet { get; }

    /// <summary>Rows from the top of a glyph to the baseline.</summary>
    public int Ascent { get; }

    /// <summary>The face's metrics as stored: the regular set has line height then ascent, the bold set its ascent.</summary>
    public IReadOnlyList<int> Metrics { get; }

    /// <summary>
    /// The sheet run through the 2x upscaler (twice as wide and tall):
    /// coverage 0 to 255 for true-color drawing, and a 1-bit mask (1 =
    /// ink, at half cover) for the palette framebuffer. Made on first use.
    /// </summary>
    public (byte[] Coverage, byte[] Mask) Upscaled
    {
        get
        {
            if (field.Coverage is null)
            {
                var white = new uint[Sheet.Pixels.Length];
                for (var i = 0; i < white.Length; i++)
                {
                    white[i] = Sheet.Pixels[i] == 1 ? 0xFFFFFFFFu : 0x00FFFFFFu;
                }

                var up = Data.Upscale.Xbrz2x.Scale(white, Sheet.Width, Sheet.Height);
                var coverage = up.Select(p => (byte)(p >> 24)).ToArray();
                field = (coverage, [.. coverage.Select(c => c >= 128 ? (byte)1 : (byte)0)]);
            }

            return field;
        }
    }

    /// <summary>How many rows a capital H stands above the baseline, measured from its ink; the ascent when there is no H.</summary>
    public int CapHeight
    {
        get
        {
            if (field == 0)
            {
                field = Ascent;
                if (TryGet('H', out var g))
                {
                    for (var row = 0; row < g.Rows; row++)
                    {
                        if (Enumerable.Range(0, g.Width).Any(col => Sheet.Pixels[(g.Y + row) * Sheet.Width + g.X + col] == 1))
                        {
                            field = Math.Max(1, Ascent - row);
                            break;
                        }
                    }
                }
            }

            return field;
        }
    }

    public bool TryGet(int code, out GlyphBox glyph)
    {
        return _glyphs.TryGetValue(code, out glyph);
    }

    /// <summary>Reads one [regular] or [bold] table of a converted fonts/pftN.toml.</summary>
    /// <param name="ascentMetric">Which metric is the ascent: 1 for the regular set, 0 for bold.</param>
    public static FontFace Load(string folder, Dictionary<string, object> table, int ascentMetric)
    {
        var sheet = PngReader.Read(File.ReadAllBytes(Path.Combine(folder, TomlReader.String(table, "sheet"))));
        var metrics = (List<object>)table["metrics"];
        var glyphs = new Dictionary<int, GlyphBox>();
        foreach (var g in TomlReader.Tables(table, "glyph"))
        {
            glyphs[TomlReader.Int(g, "code")] = new GlyphBox(
                TomlReader.Int(g, "x"), TomlReader.Int(g, "y"), TomlReader.Int(g, "width"), TomlReader.Int(g, "rows"));
        }

        var values = metrics.Select(m => checked((int)(long)m)).ToList();
        return new FontFace(sheet, glyphs, values[ascentMetric], values);
    }
}
