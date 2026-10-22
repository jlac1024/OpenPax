using System.Buffers.Binary;
using System.Text;

namespace OpenPax.Sim.Saving.Original;

/// <summary>
/// The original's save stream (docs/formats/saves.md): little endian after
/// an "II" mark, or byte swapped after "MM" (apparently Mac saves). Strings
/// are an int16 length and the bytes, read as Windows-1252. End markers are
/// read and not checked, as the original does.
/// </summary>
internal sealed class OriginalStream
{
    public const int EndMarker = 0x7CD;

    private readonly BinaryReader _in;
    private bool _swap;

    public OriginalStream(Stream stream)
    {
        _in = new BinaryReader(stream, Encoding.Latin1, leaveOpen: true);
    }

    /// <summary>The byte order mark: false when the file does not start with one.</summary>
    public bool ReadMark()
    {
        var mark = _in.ReadUInt16();
        _swap = mark == 0x4D4D;
        return mark is 0x4949 or 0x4D4D;
    }

    public int Int32()
    {
        var v = _in.ReadInt32();
        return _swap ? BinaryPrimitives.ReverseEndianness(v) : v;
    }

    public short Int16()
    {
        var v = _in.ReadInt16();
        return _swap ? BinaryPrimitives.ReverseEndianness(v) : v;
    }

    public byte Byte() => _in.ReadByte();

    public sbyte SByte() => _in.ReadSByte();

    public bool Bool() => _in.ReadByte() != 0;

    /// <summary>A double: two int32, the low half first.</summary>
    public double Double()
    {
        var low = (uint)Int32();
        var high = (uint)Int32();
        return BitConverter.Int64BitsToDouble((long)(((ulong)high << 32) | low));
    }

    public string String()
    {
        var length = Int16();
        var bytes = _in.ReadBytes(Math.Max(0, (int)length));
        return Windows1252(bytes);
    }

    public byte[] Bytes(int count) => _in.ReadBytes(count);

    /// <summary>Reads an end marker and lets it be, as every original reader does.</summary>
    public void End() => Int32();

    /// <summary>The bytes as Windows-1252: Latin-1 with the 0x80 to 0x9F punctuation.</summary>
    private static string Windows1252(byte[] bytes)
    {
        var chars = new char[bytes.Length];
        for (var i = 0; i < bytes.Length; i++)
        {
            chars[i] = bytes[i] is >= 0x80 and <= 0x9F ? _high[bytes[i] - 0x80] : (char)bytes[i];
        }

        return new string(chars);
    }

    private static readonly char[] _high =
    [
        '€', '\u0081', '‚', 'ƒ', '„', '…', '†', '‡', 'ˆ', '‰', 'Š', '‹', 'Œ', '\u008D', 'Ž', '\u008F',
        '\u0090', '‘', '’', '“', '”', '•', '–', '—', '˜', '™', 'š', '›', 'œ', '\u009D', 'ž', 'Ÿ',
    ];
}
