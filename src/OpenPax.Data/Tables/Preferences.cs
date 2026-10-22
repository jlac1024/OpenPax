using System.Buffers.Binary;
using OpenPax.Data.Resources;
using OpenPax.Data.Text;

namespace OpenPax.Data.Tables;

/// <summary>
/// <c>Data/PrefData</c>: the original's settings as typed name/value pairs.
/// The port keeps its own settings in TOML; this is read to learn the
/// original's defaults (GOG ships its own copy of the file).
/// </summary>
public static class Preferences
{
    private const int StringType = 2;
    private const int IntType = 3;
    private const int FlagType = 8;

    /// <summary>Values are <see cref="string"/>, <see cref="int"/> or <see cref="bool"/>, in file order.</summary>
    public static IReadOnlyList<KeyValuePair<string, object>> Parse(ReadOnlySpan<byte> file)
    {
        if (file.Length < 6 || file[0] != (byte)'I' || file[1] != (byte)'I')
        {
            throw new ResourceFormatException(Strings.BadPreferences);
        }

        var count = BinaryPrimitives.ReadInt32LittleEndian(file[2..]);
        var pos = 6;
        var result = new List<KeyValuePair<string, object>>();
        for (var i = 0; i < count; i++)
        {
            var name = ReadString(file, ref pos);
            var type = ReadInt32(file, ref pos);
            object value = type switch
            {
                StringType => ReadString(file, ref pos),
                IntType => ReadInt32(file, ref pos),
                FlagType => ReadByte(file, ref pos) != 0,
                _ => throw new ResourceFormatException(Strings.BadPreferences),
            };
            result.Add(new(name, value));
        }

        if (pos != file.Length)
        {
            throw new ResourceFormatException(Strings.BadPreferences);
        }

        return result;
    }

    private static string ReadString(ReadOnlySpan<byte> file, ref int pos)
    {
        if (file.Length - pos < 2)
        {
            throw new ResourceFormatException(Strings.BadPreferences);
        }

        var length = BinaryPrimitives.ReadUInt16LittleEndian(file[pos..]);
        if (file.Length - pos - 2 < length)
        {
            throw new ResourceFormatException(Strings.BadPreferences);
        }

        var s = Windows1252.GetString(file.Slice(pos + 2, length));
        pos += 2 + length;
        return s;
    }

    private static int ReadInt32(ReadOnlySpan<byte> file, ref int pos)
    {
        if (file.Length - pos < 4)
        {
            throw new ResourceFormatException(Strings.BadPreferences);
        }

        var v = BinaryPrimitives.ReadInt32LittleEndian(file[pos..]);
        pos += 4;
        return v;
    }

    private static byte ReadByte(ReadOnlySpan<byte> file, ref int pos)
    {
        if (file.Length - pos < 1)
        {
            throw new ResourceFormatException(Strings.BadPreferences);
        }

        return file[pos++];
    }
}
