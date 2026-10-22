using System.Buffers.Binary;
using OpenPax.Data.Text;

namespace OpenPax.Data.Resources;

/// <summary>
/// Sequential little-endian reads over a resource body, matching the
/// original's stream primitives. Reading past the end throws with the
/// message given for the record type being read.
/// </summary>
internal ref struct RecordReader(ReadOnlySpan<byte> body, string error)
{
    private readonly ReadOnlySpan<byte> _body = body;
    private readonly string _error = error;
    private int _pos;

    public RecordReader(ReadOnlySpan<byte> body)
        : this(body, Strings.BadTech)
    {
    }

    public readonly bool AtEnd => _pos == _body.Length;

    public int Int32() => BinaryPrimitives.ReadInt32LittleEndian(Take(4));

    public short Int16() => BinaryPrimitives.ReadInt16LittleEndian(Take(2));

    public byte Byte() => Take(1)[0];

    public double Double() => BinaryPrimitives.ReadDoubleLittleEndian(Take(8));

    /// <summary>A string as the original streams it: int16 length, then the bytes.</summary>
    public string String()
    {
        var length = Int16();
        if (length < 0)
        {
            throw new ResourceFormatException(_error);
        }

        return Windows1252.GetString(Take(length));
    }

    private ReadOnlySpan<byte> Take(int n)
    {
        if (_body.Length - _pos < n)
        {
            throw new ResourceFormatException(_error);
        }

        var s = _body.Slice(_pos, n);
        _pos += n;
        return s;
    }
}
