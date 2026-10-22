using System.Buffers.Binary;

namespace OpenPax.Media.Wav;

/// <summary>
/// Reads RIFF WAVE files with PCM data, which is every sound in the game.
/// Six GOG files carry two stray bytes after the declared RIFF size; they
/// are ignored, as Miles ignores them.
/// </summary>
public static class WavReader
{
    private const int PcmFormat = 1;

    public static PcmSound Read(ReadOnlySpan<byte> file)
    {
        if (file.Length < 12 || !file[..4].SequenceEqual("RIFF"u8) || !file[8..12].SequenceEqual("WAVE"u8))
        {
            throw new InvalidDataException(Strings.NotWav);
        }

        var riffEnd = (int)Math.Min(file.Length, 8L + BinaryPrimitives.ReadUInt32LittleEndian(file[4..]));
        int rate = 0, channels = 0, bits = 0;
        byte[]? data = null;
        var pos = 12;
        while (riffEnd - pos >= 8)
        {
            var id = file.Slice(pos, 4);
            var size = BinaryPrimitives.ReadUInt32LittleEndian(file[(pos + 4)..]);
            if (size > (uint)(riffEnd - pos - 8))
            {
                throw new InvalidDataException(Strings.BadWav);
            }

            var body = file.Slice(pos + 8, (int)size);
            if (id.SequenceEqual("fmt "u8))
            {
                if (body.Length < 16 || BinaryPrimitives.ReadUInt16LittleEndian(body) != PcmFormat)
                {
                    throw new InvalidDataException(Strings.NotPcm);
                }

                channels = BinaryPrimitives.ReadUInt16LittleEndian(body[2..]);
                rate = BinaryPrimitives.ReadInt32LittleEndian(body[4..]);
                bits = BinaryPrimitives.ReadUInt16LittleEndian(body[14..]);
            }
            else if (id.SequenceEqual("data"u8))
            {
                data = body.ToArray();
            }

            // Chunks are padded to even sizes.
            pos += 8 + (int)size + (int)(size & 1);
        }

        if (data is null || rate <= 0 || channels is < 1 or > 2 || bits is not (8 or 16))
        {
            throw new InvalidDataException(Strings.BadWav);
        }

        // Drop a trailing partial frame rather than reading past it.
        var frameBytes = channels * bits / 8;
        if (data.Length % frameBytes != 0)
        {
            Array.Resize(ref data, data.Length - data.Length % frameBytes);
        }

        return new PcmSound(rate, channels, bits, data);
    }
}
