using System.Buffers.Binary;
using OpenPax.Data.Resources;

namespace OpenPax.Data.Art;

/// <summary>A 256 color palette (<c>CPLT</c> resource, class 1000).</summary>
public sealed class Palette
{
    public const int Size = 256;
    private const int BodyLength = 4 + Size * 3;

    private readonly Rgb[] _colors;

    public Palette(Rgb[] colors)
    {
        if (colors.Length != Size)
        {
            throw new ArgumentException(string.Format(Strings.BadPaletteSize, colors.Length), nameof(colors));
        }

        _colors = colors;
    }

    public Rgb this[int index] => _colors[index];

    public IReadOnlyList<Rgb> Colors => _colors;

    // (reads 256 colors, three bytes each via)
    public static Palette Parse(ReadOnlySpan<byte> body)
    {
        if (body.Length < BodyLength || BinaryPrimitives.ReadInt32LittleEndian(body) != ClassIds.Palette)
        {
            throw new ResourceFormatException(Strings.BadPalette);
        }

        var colors = new Rgb[Size];
        for (var i = 0; i < Size; i++)
        {
            var at = 4 + i * 3;
            colors[i] = new Rgb(body[at], body[at + 1], body[at + 2]);
        }

        // The original also marks entries 1 to 254 as PC_RESERVED for
        // palette animation; that is a DirectDraw detail with no data.
        return new Palette(colors);
    }
}
