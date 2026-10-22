using System.Buffers.Binary;
using OpenPax.Data.Resources;

namespace OpenPax.Data.Text;

/// <summary>
/// A <c>STR#</c> resource (class 1005): a numbered list of strings, as on
/// the Mac. Strings are referenced by resource id and index.
/// </summary>
public static class StringList
{
    // (read: count, total size, then length-prefixed strings)
    public static IReadOnlyList<string> Parse(ReadOnlySpan<byte> body)
    {
        if (body.Length < 12 || BinaryPrimitives.ReadInt32LittleEndian(body) != ClassIds.StringList)
        {
            throw new ResourceFormatException(Strings.BadStringList);
        }

        var count = BinaryPrimitives.ReadInt32LittleEndian(body[4..]);
        var size = BinaryPrimitives.ReadInt32LittleEndian(body[8..]);
        if (count < 0 || size < 0 || size > body.Length - 12)
        {
            throw new ResourceFormatException(Strings.BadStringList);
        }

        var data = body.Slice(12, size);
        var strings = new string[count];
        var pos = 0;
        for (var i = 0; i < count; i++)
        {
            if (data.Length - pos < 2)
            {
                throw new ResourceFormatException(Strings.BadStringList);
            }

            var length = BinaryPrimitives.ReadUInt16LittleEndian(data[pos..]);
            if (data.Length - pos - 2 < length)
            {
                throw new ResourceFormatException(Strings.BadStringList);
            }

            // Every GOG string is plain ASCII; the fonts are laid out in
            // Windows-1252, so that is how any other byte is read.
            strings[i] = Windows1252.GetString(data.Slice(pos + 2, length));
            pos += 2 + length;
        }

        return strings;
    }
}
