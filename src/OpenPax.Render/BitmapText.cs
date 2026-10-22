using OpenPax.Data.Text;
using OpenPax.Data.Toml;

namespace OpenPax.Render;

/// <summary>A game font (regular and bold faces) and the original's text drawing.</summary>
public sealed class BitmapText
{
    private BitmapText(FontFace? regular, FontFace? bold)
    {
        Regular = regular;
        Bold = bold;
    }

    public FontFace? Regular { get; }

    public FontFace? Bold { get; }

    /// <summary>The look its library draws text in; null draws the original way.</summary>
    public TextSettings? Settings { get; internal set; }

    /// <summary>The modern font's pixel size standing in for this font at a scale: its capitals as tall as these.</summary>
    public int ModernSize(int scale, bool bold) =>
        Settings?.Modern?.SizeForCapitals((((bold ? Bold : Regular) ?? Regular ?? Bold)?.CapHeight ?? _capHeight) * scale) ?? 8;

    // For a face with no game font behind it (ModernOnly): its capitals' height.
    private int _capHeight = 8;

    /// <summary>
    /// A face with no game font behind it, drawn only in the modern font,
    /// its capitals this tall: for the first-run setup, which runs before the
    /// game's own fonts are imported.
    /// </summary>
    public static BitmapText ModernOnly(TextSettings settings, int capHeight) => new(null, null) { Settings = settings, _capHeight = capHeight };

    /// <summary>
    /// A line's width in the game font's pixels as it is drawn at a scale:
    /// the modern font's own width when that is the look, else the game
    /// font's. Forms lay out with it, so they need no shrinking.
    /// </summary>
    public int MeasureDrawn(string text, bool bold, int scale)
    {
        if (Settings is { Mode: TextMode.Modern, Modern: { } modern })
        {
            var s = Math.Max(1, scale);
            return (modern.Measure(text, ModernSize(s, bold), bold) + s - 1) / s;
        }

        return Measure(text, bold ? 1 : 0);
    }

    /// <summary>
    /// How far text reaches above its baseline as it is drawn at a scale, in
    /// the game font's pixels (rounded up): the modern font's ascender when
    /// that is the look, else the face's own ascent. Forms place lines by it.
    /// </summary>
    public int DrawnAscent(bool bold, int scale)
    {
        if (Settings is { Mode: TextMode.Modern, Modern: { } modern })
        {
            var s = Math.Max(1, scale);
            return (modern.Ascent(ModernSize(s, bold), bold) + s - 1) / s;
        }

        return AscentFor(bold ? 1 : 0);
    }

    /// <summary>A line's height as drawn at a scale, in the game font's pixels: the modern font's ascender and descender, else <see cref="LineHeight"/>.</summary>
    public int DrawnLineHeight(int scale)
    {
        if (Settings is { Mode: TextMode.Modern, Modern: { } modern })
        {
            var s = Math.Max(1, scale);
            var size = Math.Max(ModernSize(s, false), ModernSize(s, true));
            return (Math.Max(modern.Ascent(size, false), modern.Ascent(size, true)) + Math.Max(modern.Descent(size, false), modern.Descent(size, true)) + s - 1) / s;
        }

        return LineHeight;
    }

    /// <summary>
    /// The modern size for a line: <see cref="ModernSize"/>, made smaller
    /// (to four fifths at most) while the line is wider than the game font
    /// sets it, as every box was laid out for the game font's widths.
    /// </summary>
    public int ModernSizeFor(string text, int scale, bool bold)
    {
        var size = ModernSize(scale, bold);
        if (Settings?.Modern is not { } modern)
        {
            return size;
        }

        var room = Measure(bold ? Bold : Regular ?? Bold, text) * scale;
        var least = size * 4 / 5;
        while (size > least && modern.Measure(text, size, bold) > room)
        {
            size--;
        }

        return size;
    }

    /// <summary>Line height: the regular face's metric 0 (font +0x20).</summary>
    public int LineHeight => Regular is { Metrics.Count: > 1 } r ? r.Metrics[0] : (Regular ?? Bold)?.Ascent ?? 0;

    /// <summary>
    /// The ascent single line text is placed by: the bold face's when the
    /// style is bold and the font has one, else the regular face's.
    /// </summary>
    public int AscentFor(int style)
    {
        return ((style & 1) != 0 && Bold is not null ? Bold : Regular ?? Bold)?.Ascent ?? 0;
    }

    /// <summary>The width text takes, measured as <see cref="Draw"/> measures it for alignment.</summary>
    public int Measure(string text, int style)
    {
        if (Bold is null)
        {
            style &= 4;
        }

        return Measure(style == 1 ? Bold : Regular ?? Bold, text);
    }

    public static BitmapText Load(string tomlPath)
    {
        var folder = Path.GetDirectoryName(tomlPath) ?? ".";
        var doc = TomlReader.Parse(File.ReadAllText(tomlPath));
        var regular = doc.TryGetValue("regular", out var r) ? FontFace.Load(folder, (Dictionary<string, object>)r, 1) : null;
        var bold = doc.TryGetValue("bold", out var b) ? FontFace.Load(folder, (Dictionary<string, object>)b, 0) : null;
        return new BitmapText(regular, bold);
    }

    /// <summary>
    /// Draws text with its baseline at y. Style bit 1 picks the bold face,
    /// bit 4 adds a shadow at (+1, +1) drawn first. Align 0 left, 1 center, 2 right.
    /// </summary>
    public void Draw(Framebuffer target, string text, int x, int y, int style, int align, byte color, byte shadow)
    {
        if (Bold is null)
        {
            style &= 4;
        }

        var face = ((style & 1) != 0 ? Bold : Regular) ?? Bold ?? Regular;
        if (face is null)
        {
            return;
        }

        if (Settings is { } settings && (settings.Mode != TextMode.Original || settings.Outline != TextOutline.None))
        {
            DrawInk(target, settings, text, x, y, style, align, color, shadow);
            return;
        }

        // Quirk kept: the width is measured with the bold face only when
        // the style is exactly 1, so shadowed bold text is centered by its
        // regular width.
        var width = Measure(style == 1 ? Bold : Regular ?? Bold, text);
        if (align == 1)
        {
            x -= width / 2;
        }
        else if (align == 2)
        {
            x -= width;
        }

        if ((style & 4) != 0)
        {
            DrawRun(target, face, text, x + 1, y + 1, shadow);
        }

        DrawRun(target, face, text, x, y, color);
    }

    private static int Measure(FontFace? face, string text)
    {
        if (face is null)
        {
            return 0;
        }

        var width = 0;
        foreach (var c in text)
        {
            if (Windows1252.GetByte(c) is { } code && face.TryGet(code, out var g) && g.Width > 0)
            {
                width += g.Width - 1;
            }
        }

        return width;
    }

    private static void DrawRun(Framebuffer target, FontFace face, string text, int x, int y, byte color)
    {
        var top = y - face.Ascent;
        foreach (var c in text)
        {
            if (Windows1252.GetByte(c) is not { } code || !face.TryGet(code, out var g) || g.Width == 0)
            {
                continue;
            }

            target.DrawMask(face.Sheet.Pixels, face.Sheet.Width, g.X, g.Y, g.Width, g.Rows, x, top, color);
            x += g.Width - 1;
        }
    }

    /// <summary>
    /// Text the port's way into a palette frame: the line's glyphs in the
    /// chosen look, at the frame's scale, placed by the same point and
    /// alignment; with an outline, first every glyph's ring in black, then
    /// the glyphs themselves (white, or their own color).
    /// </summary>
    private void DrawInk(Framebuffer target, TextSettings settings, string text, int x, int y, int style, int align, byte color, byte shadow)
    {
        var bold = (style & 1) != 0;
        var scale = target.Scale;
        var width = settings.Mode == TextMode.Modern && settings.Modern is not null
            ? settings.Modern.Measure(text, ModernSizeFor(text, scale, bold), bold)
            : Measure(style == 1 ? Bold : Regular ?? Bold, text) * scale;
        var px = x * scale - (align == 1 ? width / 2 : align == 2 ? width : 0);
        var glyphs = Glyphs(text, px, y * scale, scale, bold);

        // Dark text keeping its color: over gray art (button captions on
        // silver) it is drawn plain, as a black ring only muddies it; over a
        // colored highlight (a picked row's green bar) it turns white with
        // the ring, as dark on bright color reads poorly.
        var outline = settings.Outline;
        if (outline == TextOutline.KeepColors && target.Palette is { } palette && IsDark(palette.Colors[color].R, palette.Colors[color].G, palette.Colors[color].B))
        {
            outline = glyphs.Count > 0 && Colorful(target, glyphs) ? TextOutline.White : TextOutline.None;
        }

        if (outline == TextOutline.None)
        {
            if ((style & 4) != 0)
            {
                foreach (var g in glyphs)
                {
                    target.DrawCoverage(g.Coverage, g.Width, g.Height, g.X + scale, g.Y + scale, shadow);
                }
            }

            foreach (var g in glyphs)
            {
                target.DrawCoverage(g.Coverage, g.Width, g.Height, g.X, g.Y, color);
            }

            return;
        }

        var radius = scale;
        var ink = target.Nearest(0, 0, 0);
        foreach (var g in glyphs)
        {
            target.DrawCoverage(Ring(g, radius), g.Width + radius * 2, g.Height + radius * 2, g.X - radius, g.Y - radius, ink);
        }

        var fill = outline == TextOutline.White ? target.Nearest(255, 255, 255) : color;
        foreach (var g in glyphs)
        {
            target.DrawCoverage(g.Coverage, g.Width, g.Height, g.X, g.Y, fill);
        }
    }

    /// <summary>Whether what lies under a line's glyphs is strongly colored rather than gray: the mean spread between its brightest and dullest channel.</summary>
    private static bool Colorful(Framebuffer target, IReadOnlyList<Fonts.InkGlyph> glyphs)
    {
        var left = glyphs.Min(g => g.X);
        var top = glyphs.Min(g => g.Y);
        var right = glyphs.Max(g => g.X + g.Width);
        var bottom = glyphs.Max(g => g.Y + g.Height);
        return target.Chroma(left, top, right - left, bottom - top) >= 70;
    }

    /// <summary>Whether a text color is dark enough to be drawn without the outline when it keeps its color.</summary>
    public static bool IsDark(byte r, byte g, byte b) => 0.2126 * r + 0.7152 * g + 0.0722 * b < 100;

    private readonly Dictionary<(char, int, bool, TextMode), byte[]?> _bitmapGlyphs = [];
    private readonly Dictionary<(byte[], int), byte[]> _rings = new(new RingKey());

    /// <summary>
    /// A line's glyphs at a pixel scale, left edge at px, baseline at
    /// baseline (pixels): the modern font's, the upscaled game font's at
    /// 2x, or the game font's own with each pixel a square.
    /// </summary>
    /// <param name="fit">Shrink the modern font to the game font's width (the original screens' fixed boxes); forms lay out with the drawn width instead.</param>
    public IReadOnlyList<Fonts.InkGlyph> Glyphs(string text, int px, int baseline, int scale, bool bold, bool fit = true)
    {
        var list = new List<Fonts.InkGlyph>();
        if (Settings is { Mode: TextMode.Modern, Modern: { } modern })
        {
            var size = fit ? ModernSizeFor(text, scale, bold) : ModernSize(scale, bold);
            var pen = (float)px;
            foreach (var c in text)
            {
                var g = modern.Glyph(c, size, bold);
                if (g.Width > 0)
                {
                    list.Add(new Fonts.InkGlyph(g.Coverage, g.Width, g.Height, (int)MathF.Round(pen) + g.Left, baseline - g.Top));
                }

                pen += g.Advance;
            }

            return list;
        }

        var face = (bold ? Bold : Regular) ?? Bold ?? Regular;
        if (face is null)
        {
            return list;
        }

        var mode = Settings?.Mode == TextMode.Upscaled && scale == 2 ? TextMode.Upscaled : TextMode.Original;
        var x = px;
        var top = baseline - face.Ascent * scale;
        foreach (var c in text)
        {
            if (Windows1252.GetByte(c) is not { } code || !face.TryGet(code, out var g) || g.Width == 0)
            {
                continue;
            }

            if (BitmapGlyph(face, c, g, scale, bold, mode) is { } coverage)
            {
                list.Add(new Fonts.InkGlyph(coverage, g.Width * scale, g.Rows * scale, x, top));
            }

            x += (g.Width - 1) * scale;
        }

        return list;
    }

    /// <summary>A game font glyph as coverage at a scale: its 2x upscaled cut, or its mask with each pixel a square.</summary>
    private byte[]? BitmapGlyph(FontFace face, char c, GlyphBox g, int scale, bool bold, TextMode mode)
    {
        var key = (c, scale, bold, mode);
        if (_bitmapGlyphs.TryGetValue(key, out var known))
        {
            return known;
        }

        var (w, h) = (g.Width * scale, g.Rows * scale);
        var coverage = new byte[w * h];
        var up = mode == TextMode.Upscaled ? face.Upscaled.Coverage : null;
        for (var row = 0; row < h; row++)
        {
            for (var col = 0; col < w; col++)
            {
                coverage[row * w + col] = up is not null
                    ? up[(g.Y * 2 + row) * face.Sheet.Width * 2 + g.X * 2 + col]
                    : face.Sheet.Pixels[(g.Y + row / scale) * face.Sheet.Width + g.X + col / scale] == 1 ? (byte)255 : (byte)0;
            }
        }

        _bitmapGlyphs[key] = coverage;
        return coverage;
    }

    /// <summary>A glyph's ring for the outline, grown once and kept.</summary>
    public byte[] Ring(Fonts.InkGlyph g, int radius)
    {
        if (!_rings.TryGetValue((g.Coverage, radius), out var ring))
        {
            ring = Fonts.Outline.Grow(g.Coverage, g.Width, g.Height, radius);
            _rings[(g.Coverage, radius)] = ring;
        }

        return ring;
    }

    /// <summary>Rings are kept by the glyph's own coverage array, not its contents.</summary>
    private sealed class RingKey : IEqualityComparer<(byte[], int)>
    {
        public bool Equals((byte[], int) a, (byte[], int) b) => ReferenceEquals(a.Item1, b.Item1) && a.Item2 == b.Item2;

        public int GetHashCode((byte[], int) k) => HashCode.Combine(System.Runtime.CompilerServices.RuntimeHelpers.GetHashCode(k.Item1), k.Item2);
    }
}
