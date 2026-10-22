using System.Buffers.Binary;
using OpenPax.Data.Resources;

namespace OpenPax.Data.Text;

/// <summary>
/// A <c>Data/pft&lt;size&gt;</c> bitmap font: a regular and a bold glyph set
/// (either may be absent), each covering a
/// range of character codes. Layout is in docs/formats/fonts.md.
/// </summary>
public sealed class BitmapFont
{
    private BitmapFont(GlyphSet? regular, GlyphSet? bold)
    {
        Regular = regular;
        Bold = bold;
    }

    /// <summary>The regular weight, stored first (slots code + 2 in the original).</summary>
    public GlyphSet? Regular { get; }

    /// <summary>The bold weight, stored second (slots code + 0x101 in the original).</summary>
    public GlyphSet? Bold { get; }

    // (load; the original keeps the result of "has either set")
    public static BitmapFont Parse(ReadOnlySpan<byte> file)
    {
        if (file.Length < 2 || file[0] != (byte)'I' || file[1] != (byte)'I')
        {
            throw new ResourceFormatException(Strings.BadFont);
        }

        var pos = 2;
        var regular = ReadSet(file, ref pos, metricCount: 2);
        var bold = ReadSet(file, ref pos, metricCount: 1);
        if (pos != file.Length)
        {
            throw new ResourceFormatException(Strings.BadFont);
        }

        return new BitmapFont(regular, bold);
    }

    private static GlyphSet? ReadSet(ReadOnlySpan<byte> file, ref int pos, int metricCount)
    {
        int first = ReadInt16(file, ref pos);
        int last = ReadInt16(file, ref pos);
        var metrics = new int[metricCount];
        for (var i = 0; i < metricCount; i++)
        {
            metrics[i] = ReadInt32(file, ref pos);
        }

        // A negative first code means the set is absent, but its metrics
        // are still stored.
        if (first < 0)
        {
            return null;
        }

        if (last > 0xFF || last < first - 1)
        {
            throw new ResourceFormatException(Strings.BadFont);
        }

        var glyphs = new List<Glyph>();
        for (var code = first; code <= last; code++)
        {
            var width = ReadInt32(file, ref pos);
            if (width == 0)
            {
                glyphs.Add(new Glyph(code, 0, []));
                continue;
            }

            var size = ReadInt32(file, ref pos);
            if (width < 0 || size < 0 || size > file.Length - pos || size % ((width + 7) >> 3) != 0)
            {
                throw new ResourceFormatException(Strings.BadFont);
            }

            glyphs.Add(new Glyph(code, width, file.Slice(pos, size).ToArray()));
            pos += size;
        }

        return new GlyphSet(first, last, metrics, glyphs);
    }

    private static short ReadInt16(ReadOnlySpan<byte> file, ref int pos)
    {
        if (file.Length - pos < 2)
        {
            throw new ResourceFormatException(Strings.BadFont);
        }

        var v = BinaryPrimitives.ReadInt16LittleEndian(file[pos..]);
        pos += 2;
        return v;
    }

    private static int ReadInt32(ReadOnlySpan<byte> file, ref int pos)
    {
        if (file.Length - pos < 4)
        {
            throw new ResourceFormatException(Strings.BadFont);
        }

        var v = BinaryPrimitives.ReadInt32LittleEndian(file[pos..]);
        pos += 4;
        return v;
    }
}
