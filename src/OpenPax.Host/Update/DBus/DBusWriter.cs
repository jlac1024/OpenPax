using System.Text;

namespace OpenPax.Host.Update.DBus;

/// <summary>
/// D-Bus values written in little-endian wire format, each aligned to its
/// size from the start of the message (the D-Bus specification, "Marshaling").
/// </summary>
internal sealed class DBusWriter
{
    private readonly List<byte> _bytes = [];

    public int Length => _bytes.Count;

    public byte[] ToArray() => [.. _bytes];

    public void Pad(int alignment)
    {
        while (_bytes.Count % alignment != 0)
        {
            _bytes.Add(0);
        }
    }

    public void Byte(byte value) => _bytes.Add(value);

    public void UInt32(uint value)
    {
        Pad(4);
        _bytes.AddRange(BitConverter.GetBytes(value));
    }

    /// <summary>A string or object path: its length, the UTF-8 bytes, a NUL.</summary>
    public void String(string value)
    {
        var bytes = Encoding.UTF8.GetBytes(value);
        UInt32((uint)bytes.Length);
        _bytes.AddRange(bytes);
        _bytes.Add(0);
    }

    /// <summary>A signature: a one-byte length, the bytes, a NUL.</summary>
    public void Signature(string value)
    {
        _bytes.Add((byte)value.Length);
        _bytes.AddRange(Encoding.ASCII.GetBytes(value));
        _bytes.Add(0);
    }

    /// <summary>An empty a{sv} (the options dictionary of the portal's methods).</summary>
    public void EmptyDictionary()
    {
        UInt32(0);
        Pad(8);
    }

    /// <summary>Patches a 32-bit value written earlier (an array's length, known only after its items).</summary>
    public void SetUInt32(int at, uint value)
    {
        var bytes = BitConverter.GetBytes(value);
        for (var i = 0; i < 4; i++)
        {
            _bytes[at + i] = bytes[i];
        }
    }
}
