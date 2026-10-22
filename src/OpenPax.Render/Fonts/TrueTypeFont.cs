using System.Buffers.Binary;

namespace OpenPax.Render.Fonts;

/// <summary>
/// A TrueType font file read for drawing (docs/formats/truetype.md): its
/// character map, glyph outlines (simple and composite, quadratic curves)
/// and advance widths. Hinting, kerning and the layout tables are not used.
/// </summary>
public sealed class TrueTypeFont
{
    private readonly byte[] _data;
    private readonly Dictionary<string, (int Offset, int Length)> _tables = [];
    private readonly Dictionary<int, int> _charToGlyph = [];
    private readonly int[] _glyphOffsets;
    private readonly ushort[] _advances;
    private readonly int _glyf;

    public TrueTypeFont(byte[] data)
    {
        _data = data;
        var count = U16(4);
        for (var i = 0; i < count; i++)
        {
            var at = 12 + i * 16;
            _tables[System.Text.Encoding.ASCII.GetString(data, at, 4)] = ((int)U32(at + 8), (int)U32(at + 12));
        }

        var head = Table("head");
        UnitsPerEm = U16(head + 18);
        var longOffsets = I16(head + 50) == 1;
        var glyphs = U16(Table("maxp") + 4);
        var hhea = Table("hhea");
        Ascender = I16(hhea + 4);
        Descender = I16(hhea + 6);
        LineGap = I16(hhea + 8);
        var metrics = U16(hhea + 34);

        var hmtx = Table("hmtx");
        _advances = new ushort[glyphs];
        for (var g = 0; g < glyphs; g++)
        {
            _advances[g] = U16(hmtx + Math.Min(g, metrics - 1) * 4);
        }

        var loca = Table("loca");
        _glyphOffsets = new int[glyphs + 1];
        for (var g = 0; g <= glyphs; g++)
        {
            _glyphOffsets[g] = longOffsets ? (int)U32(loca + g * 4) : U16(loca + g * 2) * 2;
        }

        _glyf = Table("glyf");
        ReadCmap(Table("cmap"));
    }

    public int UnitsPerEm { get; }

    public int Ascender { get; }

    /// <summary>Below the baseline, as a negative number of units.</summary>
    public int Descender { get; }

    public int LineGap { get; }

    public static TrueTypeFont Load(string path) => new(File.ReadAllBytes(path));

    public int GlyphFor(char c) => _charToGlyph.GetValueOrDefault(c);

    public int Advance(int glyph) => glyph < _advances.Length ? _advances[glyph] : 0;

    /// <summary>A glyph's outline as closed contours of points in font units (y up), curves already flattened to lines.</summary>
    public IReadOnlyList<IReadOnlyList<(float X, float Y)>> Outline(int glyph)
    {
        var contours = new List<IReadOnlyList<(float X, float Y)>>();
        AddGlyph(glyph, 1, 0, 0, 1, 0, 0, contours, 0);
        return contours;
    }

    private void AddGlyph(int glyph, float a, float b, float c, float d, float dx, float dy, List<IReadOnlyList<(float X, float Y)>> into, int depth)
    {
        if (glyph < 0 || glyph + 1 >= _glyphOffsets.Length || depth > 8)
        {
            return;
        }

        var start = _glyf + _glyphOffsets[glyph];
        if (_glyphOffsets[glyph + 1] == _glyphOffsets[glyph])
        {
            return;
        }

        var contours = I16(start);
        if (contours >= 0)
        {
            foreach (var contour in SimpleContours(start, contours))
            {
                into.Add([.. contour.Select(p => (a * p.X + c * p.Y + dx, b * p.X + d * p.Y + dy))]);
            }

            return;
        }

        // A composite: parts placed by offsets and an optional scale or 2x2 matrix.
        var at = start + 10;
        while (true)
        {
            var flags = U16(at);
            var part = U16(at + 2);
            at += 4;
            float ox, oy;
            if ((flags & 1) != 0)
            {
                (ox, oy) = (I16(at), I16(at + 2));
                at += 4;
            }
            else
            {
                (ox, oy) = ((sbyte)_data[at], (sbyte)_data[at + 1]);
                at += 2;
            }

            float pa = 1, pb = 0, pc = 0, pd = 1;
            if ((flags & 8) != 0)
            {
                pa = pd = F2Dot14(at);
                at += 2;
            }
            else if ((flags & 0x40) != 0)
            {
                (pa, pd) = (F2Dot14(at), F2Dot14(at + 2));
                at += 4;
            }
            else if ((flags & 0x80) != 0)
            {
                (pa, pb, pc, pd) = (F2Dot14(at), F2Dot14(at + 2), F2Dot14(at + 4), F2Dot14(at + 6));
                at += 8;
            }

            // Only offsets given as x and y are used; point matching is rare and left out.
            if ((flags & 2) == 0)
            {
                (ox, oy) = (0, 0);
            }

            AddGlyph(part, a * pa + c * pb, b * pa + d * pb, a * pc + c * pd, b * pc + d * pd, a * ox + c * oy + dx, b * ox + d * oy + dy, into, depth + 1);
            if ((flags & 0x20) == 0)
            {
                break;
            }
        }
    }

    /// <summary>A simple glyph's contours, quadratic curves flattened, with the implied on-curve points between off-curve ones.</summary>
    private List<List<(float X, float Y)>> SimpleContours(int start, int count)
    {
        var ends = new int[count];
        for (var i = 0; i < count; i++)
        {
            ends[i] = U16(start + 10 + i * 2);
        }

        var points = count == 0 ? 0 : ends[^1] + 1;
        var at = start + 10 + count * 2;
        at += 2 + U16(at);
        var flags = new byte[points];
        for (var i = 0; i < points;)
        {
            var f = _data[at++];
            flags[i++] = f;
            if ((f & 8) != 0)
            {
                for (var r = _data[at++]; r > 0 && i < points; r--)
                {
                    flags[i++] = f;
                }
            }
        }

        var xs = new int[points];
        var ys = new int[points];
        ReadCoordinates(flags, xs, 2, 16, ref at);
        ReadCoordinates(flags, ys, 4, 32, ref at);

        var result = new List<List<(float X, float Y)>>();
        var first = 0;
        foreach (var end in ends)
        {
            var n = end - first + 1;
            if (n > 1)
            {
                result.Add(Flatten(xs, ys, flags, first, n));
            }

            first = end + 1;
        }

        return result;
    }

    private void ReadCoordinates(byte[] flags, int[] values, int shortBit, int sameBit, ref int at)
    {
        var v = 0;
        for (var i = 0; i < flags.Length; i++)
        {
            var f = flags[i];
            if ((f & shortBit) != 0)
            {
                var d = _data[at++];
                v += (f & sameBit) != 0 ? d : -d;
            }
            else if ((f & sameBit) == 0)
            {
                v += I16(at);
                at += 2;
            }

            values[i] = v;
        }
    }

    private static List<(float X, float Y)> Flatten(int[] xs, int[] ys, byte[] flags, int first, int n)
    {
        bool On(int i) => (flags[first + ((i % n) + n) % n] & 1) != 0;
        (float X, float Y) P(int i)
        {
            var k = first + ((i % n) + n) % n;
            return (xs[k], ys[k]);
        }

        (float, float) Mid((float X, float Y) p, (float X, float Y) q) => ((p.X + q.X) / 2, (p.Y + q.Y) / 2);

        // Start from an on-curve point, or the midpoint of two off-curve ones.
        var startIndex = Enumerable.Range(0, n).FirstOrDefault(On, -1);
        var start = startIndex >= 0 ? P(startIndex) : Mid(P(0), P(1));
        var offset = startIndex >= 0 ? startIndex : 0;
        var path = new List<(float X, float Y)> { start };
        var current = start;
        (float X, float Y)? control = null;
        for (var s = 1; s <= n; s++)
        {
            var i = offset + s;
            var p = s == n && startIndex >= 0 ? start : P(i);
            if (On(i) || (s == n && startIndex >= 0))
            {
                if (control is { } ctl)
                {
                    Curve(path, current, ctl, p);
                }
                else
                {
                    path.Add(p);
                }

                current = p;
                control = null;
            }
            else if (control is { } ctl)
            {
                var mid = Mid(ctl, p);
                Curve(path, current, ctl, mid);
                current = mid;
                control = p;
            }
            else
            {
                control = p;
            }
        }

        if (control is { } last)
        {
            Curve(path, current, last, start);
        }

        return path;
    }

    private static void Curve(List<(float X, float Y)> path, (float X, float Y) a, (float X, float Y) c, (float X, float Y) b)
    {
        const int Steps = 6;
        for (var i = 1; i <= Steps; i++)
        {
            var t = i / (float)Steps;
            var u = 1 - t;
            path.Add((u * u * a.X + 2 * u * t * c.X + t * t * b.X, u * u * a.Y + 2 * u * t * c.Y + t * t * b.Y));
        }
    }

    /// <summary>The Unicode character map: format 4 (16 bit) or 12 (32 bit) subtables.</summary>
    private void ReadCmap(int cmap)
    {
        var count = U16(cmap + 2);
        var best = -1;
        for (var i = 0; i < count; i++)
        {
            var at = cmap + 4 + i * 8;
            var (platform, encoding) = (U16(at), U16(at + 2));
            var offset = cmap + (int)U32(at + 4);
            var format = U16(offset);
            if ((platform == 3 && encoding is 1 or 10) || platform == 0)
            {
                if (format == 12 || (format == 4 && best < 0))
                {
                    best = offset;
                }
            }
        }

        if (best < 0)
        {
            return;
        }

        if (U16(best) == 4)
        {
            var segments = U16(best + 6) / 2;
            var ends = best + 14;
            var starts = ends + segments * 2 + 2;
            var deltas = starts + segments * 2;
            var ranges = deltas + segments * 2;
            for (var s = 0; s < segments; s++)
            {
                int end = U16(ends + s * 2), begin = U16(starts + s * 2), delta = I16(deltas + s * 2), range = U16(ranges + s * 2);
                for (var ch = begin; ch <= end && ch != 0xFFFF; ch++)
                {
                    var glyph = range == 0 ? (ch + delta) & 0xFFFF : U16(ranges + s * 2 + range + (ch - begin) * 2);
                    if (range != 0 && glyph != 0)
                    {
                        glyph = (glyph + delta) & 0xFFFF;
                    }

                    _charToGlyph[ch] = glyph;
                }
            }
        }
        else
        {
            var groups = (int)U32(best + 12);
            for (var g = 0; g < groups; g++)
            {
                var at = best + 16 + g * 12;
                var (begin, end, glyph) = (U32(at), U32(at + 4), U32(at + 8));
                for (var ch = begin; ch <= end && ch < 0x30000; ch++)
                {
                    _charToGlyph[(int)ch] = (int)(glyph + ch - begin);
                }
            }
        }
    }

    private int Table(string tag) => _tables.TryGetValue(tag, out var t) ? t.Offset : throw new InvalidDataException("TrueType table missing: " + tag);

    private ushort U16(int at) => BinaryPrimitives.ReadUInt16BigEndian(_data.AsSpan(at));

    private short I16(int at) => BinaryPrimitives.ReadInt16BigEndian(_data.AsSpan(at));

    private uint U32(int at) => BinaryPrimitives.ReadUInt32BigEndian(_data.AsSpan(at));

    private float F2Dot14(int at) => I16(at) / 16384f;
}
