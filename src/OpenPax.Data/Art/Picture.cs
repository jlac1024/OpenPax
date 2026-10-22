using System.Buffers.Binary;
using OpenPax.Data.Resources;

namespace OpenPax.Data.Art;

/// <summary>
/// A decoded <c>PPIC</c> picture (class 1003): one palette index per pixel.
/// Index 1 means transparent, as in the original's blitters.
/// </summary>
public sealed class Picture
{
    /// <summary>Palette index the original treats as "do not draw".</summary>
    public const byte Transparent = 1;

    private const int HeaderSize = 24;
    private const int RunFlag = 0xC0;

    public Picture(int left, int top, int width, int height, byte[] pixels)
    {
        if (width < 0 || height < 0 || pixels.Length != width * height)
        {
            throw new ArgumentException(Strings.BadPictureSize, nameof(pixels));
        }

        Left = left;
        Top = top;
        Width = width;
        Height = height;
        Pixels = pixels;
    }

    /// <summary>Left of the picture's rectangle. Zero in every GOG picture.</summary>
    public int Left { get; }

    /// <summary>Top of the picture's rectangle. Zero in every GOG picture.</summary>
    public int Top { get; }

    public int Width { get; }

    public int Height { get; }

    /// <summary>Row-major palette indices, <see cref="Width"/> x <see cref="Height"/>.</summary>
    public byte[] Pixels { get; }

    // (read: rectangle, data size, row offsets, data)
    public static Picture Parse(ReadOnlySpan<byte> body)
    {
        if (body.Length < HeaderSize || BinaryPrimitives.ReadInt32LittleEndian(body) != ClassIds.Picture)
        {
            throw new ResourceFormatException(Strings.BadPicture);
        }

        var left = BinaryPrimitives.ReadInt32LittleEndian(body[4..]);
        var top = BinaryPrimitives.ReadInt32LittleEndian(body[8..]);
        var right = BinaryPrimitives.ReadInt32LittleEndian(body[12..]);
        var bottom = BinaryPrimitives.ReadInt32LittleEndian(body[16..]);
        var dataSize = BinaryPrimitives.ReadInt32LittleEndian(body[20..]);
        var width = right - left;
        var height = bottom - top;
        if (width < 0 || height < 0 || dataSize < 0 || (long)HeaderSize + 4L * height + dataSize > body.Length)
        {
            throw new ResourceFormatException(Strings.BadPicture);
        }

        var rows = body.Slice(HeaderSize, 4 * height);
        var data = body.Slice(HeaderSize + 4 * height, dataSize);
        var pixels = new byte[width * height];
        for (var y = 0; y < height; y++)
        {
            var pos = BinaryPrimitives.ReadInt32LittleEndian(rows[(4 * y)..]);
            DecodeRow(data, pos, pixels.AsSpan(y * width, width));
        }

        return new Picture(left, top, width, height, pixels);
    }

    private static void DecodeRow(ReadOnlySpan<byte> data, int pos, Span<byte> row)
    {
        // PCX style: a byte below 0xC0 is one pixel of that index; a byte
        // of 0xC0 or more is a run of (byte - 0xC0) pixels of the next byte.
        // Transparent runs (index 1) are skipped by the original, which
        // leaves the destination as it was; here they keep index 1.
        var x = 0;
        while (x < row.Length)
        {
            if ((uint)pos >= (uint)data.Length)
            {
                throw new ResourceFormatException(Strings.BadPicture);
            }

            int run = 1;
            if (data[pos] >= RunFlag)
            {
                run = data[pos] - RunFlag;
                pos++;
                if ((uint)pos >= (uint)data.Length)
                {
                    throw new ResourceFormatException(Strings.BadPicture);
                }
            }

            // A zero run would make the original's byte counter wrap to 256
            // pixels, and a run past the row end would write into the next
            // row. Neither occurs in the GOG data, so both are refused.
            if (run == 0 || x + run > row.Length)
            {
                throw new ResourceFormatException(Strings.BadPicture);
            }

            row.Slice(x, run).Fill(data[pos]);
            pos++;
            x += run;
        }
    }
}
