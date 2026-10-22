using System.Buffers.Binary;
using System.IO.Compression;

namespace OpenPax.Data.Png;

/// <summary>Writes 8-bit indexed, and 8-bit RGBA, non-interlaced PNG files.</summary>
public static class PngWriter
{
    internal static ReadOnlySpan<byte> Signature => [0x89, (byte)'P', (byte)'N', (byte)'G', 0x0D, 0x0A, 0x1A, 0x0A];

    public static void Write(Stream output, IndexedImage image)
    {
        if (image.Width < 1 || image.Height < 1 || image.Pixels.Length != image.Width * image.Height)
        {
            throw new ArgumentException(Strings.BadPictureSize, nameof(image));
        }

        output.Write(Signature);

        Span<byte> header = stackalloc byte[13];
        BinaryPrimitives.WriteInt32BigEndian(header, image.Width);
        BinaryPrimitives.WriteInt32BigEndian(header[4..], image.Height);
        header[8] = 8; // bits per index
        header[9] = 3; // color type: palette
        header[10] = 0; // deflate
        header[11] = 0; // adaptive filtering
        header[12] = 0; // no interlace
        WriteChunk(output, "IHDR"u8, header);

        var plte = new byte[image.Palette.Count * 3];
        for (var i = 0; i < image.Palette.Count; i++)
        {
            plte[i * 3] = image.Palette[i].R;
            plte[i * 3 + 1] = image.Palette[i].G;
            plte[i * 3 + 2] = image.Palette[i].B;
        }

        WriteChunk(output, "PLTE"u8, plte);
        if (image.Alpha.Count > 0)
        {
            WriteChunk(output, "tRNS"u8, [.. image.Alpha]);
        }

        WriteChunk(output, "IDAT"u8, Compress(image));
        WriteChunk(output, "IEND"u8, []);
    }

    /// <summary>A true-color picture, bytes R, G, B, A per pixel row by row (screenshots of the whole window).</summary>
    public static void WriteRgba(Stream output, int width, int height, ReadOnlySpan<byte> rgba)
    {
        if (width < 1 || height < 1 || rgba.Length != width * height * 4)
        {
            throw new ArgumentException(Strings.BadPictureSize, nameof(rgba));
        }

        output.Write(Signature);
        Span<byte> header = stackalloc byte[13];
        BinaryPrimitives.WriteInt32BigEndian(header, width);
        BinaryPrimitives.WriteInt32BigEndian(header[4..], height);
        header[8] = 8; // bits per channel
        header[9] = 6; // color type: RGBA
        header[10] = 0;
        header[11] = 0;
        header[12] = 0;
        WriteChunk(output, "IHDR"u8, header);
        using var buffer = new MemoryStream();
        using (var z = new ZLibStream(buffer, CompressionLevel.Fastest, leaveOpen: true))
        {
            for (var y = 0; y < height; y++)
            {
                z.WriteByte(0);
                z.Write(rgba.Slice(y * width * 4, width * 4));
            }
        }

        WriteChunk(output, "IDAT"u8, buffer.ToArray());
        WriteChunk(output, "IEND"u8, []);
    }

    private static byte[] Compress(IndexedImage image)
    {
        using var buffer = new MemoryStream();
        using (var z = new ZLibStream(buffer, CompressionLevel.SmallestSize, leaveOpen: true))
        {
            // Filter type 0 on every row. Indexed art with large flat areas
            // compresses about as well this way as with the other filters.
            for (var y = 0; y < image.Height; y++)
            {
                z.WriteByte(0);
                z.Write(image.Pixels, y * image.Width, image.Width);
            }
        }

        return buffer.ToArray();
    }

    private static void WriteChunk(Stream output, ReadOnlySpan<byte> type, ReadOnlySpan<byte> data)
    {
        Span<byte> word = stackalloc byte[4];
        BinaryPrimitives.WriteInt32BigEndian(word, data.Length);
        output.Write(word);
        output.Write(type);
        output.Write(data);
        BinaryPrimitives.WriteUInt32BigEndian(word, Crc32.Compute(type, data));
        output.Write(word);
    }
}
