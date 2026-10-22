using System.Buffers.Binary;

namespace OpenPax.Media.Ogg;

/// <summary>
/// Splits an Ogg file into the packets of its first logical stream (RFC
/// 3533). The game's music files hold a single Vorbis stream each.
/// Page checksums are verified.
/// </summary>
public static class OggReader
{
    private const int HeaderSize = 27;

    public static IEnumerable<OggPacket> Packets(byte[] file)
    {
        var pos = 0;
        int? serial = null;
        var pending = new List<byte>();
        while (pos < file.Length)
        {
            if (file.Length - pos < HeaderSize || !file.AsSpan(pos, 4).SequenceEqual("OggS"u8) || file[pos + 4] != 0)
            {
                throw new InvalidDataException(Strings.BadOgg);
            }

            var flags = file[pos + 5];
            var granule = BinaryPrimitives.ReadInt64LittleEndian(file.AsSpan(pos + 6));
            var pageSerial = BinaryPrimitives.ReadInt32LittleEndian(file.AsSpan(pos + 14));
            var crc = BinaryPrimitives.ReadUInt32LittleEndian(file.AsSpan(pos + 22));
            var segments = file[pos + 26];
            if (file.Length - pos < HeaderSize + segments)
            {
                throw new InvalidDataException(Strings.BadOgg);
            }

            var lacing = file.AsSpan(pos + HeaderSize, segments).ToArray();
            var bodySize = 0;
            foreach (var l in lacing)
            {
                bodySize += l;
            }

            var pageSize = HeaderSize + segments + bodySize;
            if (file.Length - pos < pageSize || OggCrc.Compute(file.AsSpan(pos, pageSize)) != crc)
            {
                throw new InvalidDataException(Strings.BadOgg);
            }

            serial ??= pageSerial;
            if (pageSerial != serial)
            {
                // Pages of other logical streams are skipped.
                pos += pageSize;
                continue;
            }

            var endOfStream = (flags & 0x04) != 0;
            var body = pos + HeaderSize + segments;

            // Find which segment ends the last packet on this page, so only
            // that packet gets the page's granule position.
            var lastEnd = -1;
            for (var i = 0; i < segments; i++)
            {
                if (lacing[i] < 255)
                {
                    lastEnd = i;
                }
            }

            for (var i = 0; i < segments; i++)
            {
                pending.AddRange(file.AsSpan(body, lacing[i]).ToArray());
                body += lacing[i];
                if (lacing[i] < 255)
                {
                    var isLast = i == lastEnd;
                    yield return new OggPacket([.. pending], isLast ? granule : -1, isLast && endOfStream);
                    pending.Clear();
                }
            }

            pos += pageSize;
            if (endOfStream)
            {
                yield break;
            }
        }
    }
}
