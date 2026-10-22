using System.Buffers.Binary;
using System.IO.Compression;
using OpenPax.Data.Art;
using OpenPax.Data.Resources;

namespace OpenPax.Data.Png;

/// <summary>
/// Reads 8-bit indexed, non-interlaced PNG files: what our importer
/// writes, and what replacement art for the port must be. All five row
/// filters are handled, so files saved by image editors load too.
/// </summary>
public static class PngReader
{
    public static IndexedImage Read(byte[] file)
    {
        if (file.Length < 8 || !file.AsSpan(0, 8).SequenceEqual(PngWriter.Signature))
        {
            throw Bad("signature");
        }

        int width = 0, height = 0;
        Rgb[]? palette = null;
        byte[] alpha = [];
        using var idat = new MemoryStream();
        var pos = 8;
        var ended = false;
        while (!ended)
        {
            if (file.Length - pos < 12)
            {
                throw Bad("truncated");
            }

            var length = BinaryPrimitives.ReadInt32BigEndian(file.AsSpan(pos));
            if (length < 0 || length > file.Length - pos - 12)
            {
                throw Bad("chunk length");
            }

            var type = file.AsSpan(pos + 4, 4);
            var data = file.AsSpan(pos + 8, length);
            var crc = BinaryPrimitives.ReadUInt32BigEndian(file.AsSpan(pos + 8 + length));
            if (crc != Crc32.Compute(type, data))
            {
                throw Bad("checksum");
            }

            switch (System.Text.Encoding.ASCII.GetString(type))
            {
                case "IHDR":
                    if (length != 13 || data[8] != 8 || data[9] != 3 || data[10] != 0 || data[11] != 0 || data[12] != 0)
                    {
                        throw Bad("only 8-bit indexed, non-interlaced images are supported");
                    }

                    width = BinaryPrimitives.ReadInt32BigEndian(data);
                    height = BinaryPrimitives.ReadInt32BigEndian(data[4..]);
                    break;
                case "PLTE":
                    if (length % 3 != 0 || length > 768)
                    {
                        throw Bad("palette");
                    }

                    palette = new Rgb[length / 3];
                    for (var i = 0; i < palette.Length; i++)
                    {
                        palette[i] = new Rgb(data[i * 3], data[i * 3 + 1], data[i * 3 + 2]);
                    }

                    break;
                case "tRNS":
                    alpha = data.ToArray();
                    break;
                case "IDAT":
                    idat.Write(data);
                    break;
                case "IEND":
                    ended = true;
                    break;
                default:
                    // Ancillary chunks (lower case first letter) are skipped;
                    // an unknown critical chunk means we cannot read the file.
                    if ((type[0] & 0x20) == 0)
                    {
                        throw Bad("unknown critical chunk");
                    }

                    break;
            }

            pos += 12 + length;
        }

        if (width < 1 || height < 1 || palette is null)
        {
            throw Bad("missing header or palette");
        }

        return new IndexedImage(width, height, Unfilter(idat.ToArray(), width, height), palette, alpha);
    }

    private static byte[] Unfilter(byte[] compressed, int width, int height)
    {
        var raw = new byte[(width + 1) * height];
        using (var z = new ZLibStream(new MemoryStream(compressed), CompressionMode.Decompress))
        {
            z.ReadExactly(raw);
        }

        var pixels = new byte[width * height];
        for (var y = 0; y < height; y++)
        {
            var filter = raw[y * (width + 1)];
            var src = raw.AsSpan(y * (width + 1) + 1, width);
            var row = pixels.AsSpan(y * width, width);
            var up = y > 0 ? pixels.AsSpan((y - 1) * width, width) : default;
            for (var x = 0; x < width; x++)
            {
                // One byte per pixel, so "left" is the previous byte.
                int a = x > 0 ? row[x - 1] : 0;
                int b = y > 0 ? up[x] : 0;
                int c = x > 0 && y > 0 ? up[x - 1] : 0;
                row[x] = filter switch
                {
                    0 => src[x],
                    1 => (byte)(src[x] + a),
                    2 => (byte)(src[x] + b),
                    3 => (byte)(src[x] + (a + b) / 2),
                    4 => (byte)(src[x] + Paeth(a, b, c)),
                    _ => throw Bad("row filter"),
                };
            }
        }

        return pixels;
    }

    private static int Paeth(int a, int b, int c)
    {
        var p = a + b - c;
        var pa = Math.Abs(p - a);
        var pb = Math.Abs(p - b);
        var pc = Math.Abs(p - c);
        return pa <= pb && pa <= pc ? a : pb <= pc ? b : c;
    }

    private static ResourceFormatException Bad(string what)
    {
        return new ResourceFormatException(string.Format(Strings.BadPng, what));
    }
}
