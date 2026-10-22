using System.Buffers.Binary;
using System.Text;

namespace OpenPax.Data.Installer;

/// <summary>Reads the values an Inno Setup index is made of (docs/formats/inno-setup.md).</summary>
internal sealed class IndexReader(Stream input)
{
    private readonly byte[] _scratch = new byte[8];

    public byte U8()
    {
        input.ReadExactly(_scratch, 0, 1);
        return _scratch[0];
    }

    public ushort U16()
    {
        input.ReadExactly(_scratch, 0, 2);
        return BinaryPrimitives.ReadUInt16LittleEndian(_scratch);
    }

    public uint U32()
    {
        input.ReadExactly(_scratch, 0, 4);
        return BinaryPrimitives.ReadUInt32LittleEndian(_scratch);
    }

    public ulong U64()
    {
        input.ReadExactly(_scratch, 0, 8);
        return BinaryPrimitives.ReadUInt64LittleEndian(_scratch);
    }

    public byte[] Bytes(int count)
    {
        var bytes = new byte[count];
        input.ReadExactly(bytes);
        return bytes;
    }

    public void Skip(int count)
    {
        Span<byte> sink = stackalloc byte[256];
        while (count > 0)
        {
            var n = Math.Min(count, sink.Length);
            input.ReadExactly(sink[..n]);
            count -= n;
        }
    }

    /// <summary>A string: its length in bytes, then UTF-16 text (the Unicode builds of Inno Setup).</summary>
    public string Text()
    {
        var length = (int)U32();
        if (length < 0 || length > (64 << 20))
        {
            throw new InvalidDataException(Strings.InnoDamaged);
        }

        return Encoding.Unicode.GetString(Bytes(length));
    }

    /// <summary>Strings not needed, passed over.</summary>
    public void SkipTexts(int count)
    {
        for (var i = 0; i < count; i++)
        {
            var length = (int)U32();
            if (length < 0)
            {
                throw new InvalidDataException(Strings.InnoDamaged);
            }

            Skip(length);
        }
    }

    /// <summary>
    /// A set of <paramref name="count"/> yes/no options, a bit each, in whole
    /// bytes; a three-byte set takes four, as Delphi stores it.
    /// </summary>
    public ulong Flags(int count)
    {
        var bytes = (count + 7) / 8;
        ulong value = 0;
        for (var i = 0; i < bytes; i++)
        {
            value |= (ulong)U8() << (8 * i);
        }

        if (bytes == 3)
        {
            U8();
        }

        return value;
    }

    /// <summary>The Windows versions a part is for (two of: version, NT version, service pack).</summary>
    public void SkipWindowsVersions() => Skip(20);
}
