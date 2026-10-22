using System.Buffers.Binary;
using OpenPax.Data.Compression;
using OpenPax.Data.Png;

namespace OpenPax.Data.Installer;

/// <summary>
/// One packed block of an Inno Setup index (docs/formats/inno-setup.md):
/// a checked header giving its size, then the data in checked pieces of
/// 4096 bytes, packed with LZMA as a whole.
/// </summary>
internal static class IndexBlock
{
    private const int Piece = 4096;

    /// <summary>The block's content, from the stream's position; leaves the stream just past the block.</summary>
    public static Stream Open(Stream file)
    {
        Span<byte> head = stackalloc byte[9];
        file.ReadExactly(head);
        var crc = BinaryPrimitives.ReadUInt32LittleEndian(head);
        if (Crc32.Compute(head[4..], default) != crc)
        {
            throw new InvalidDataException(Strings.InnoDamaged);
        }

        var stored = BinaryPrimitives.ReadUInt32LittleEndian(head[4..]);
        var compressed = head[8] != 0;
        if (stored > (256u << 20))
        {
            throw new InvalidDataException(Strings.InnoDamaged);
        }

        var raw = new byte[stored];
        file.ReadExactly(raw);
        var data = new MemoryStream(raw.Length);
        for (var at = 0; at < raw.Length;)
        {
            if (raw.Length - at < 5)
            {
                throw new InvalidDataException(Strings.InnoDamaged);
            }

            var pieceCrc = BinaryPrimitives.ReadUInt32LittleEndian(raw.AsSpan(at));
            var piece = raw.AsSpan(at + 4, Math.Min(Piece, raw.Length - at - 4));
            if (Crc32.Compute(piece, default) != pieceCrc)
            {
                throw new InvalidDataException(Strings.InnoDamaged);
            }

            data.Write(piece);
            at += 4 + piece.Length;
        }

        data.Position = 0;
        return compressed ? new LzmaStream(data) : data;
    }
}
