namespace OpenPax.Media.Vorbis;

/// <summary>
/// Reads a Vorbis packet bit by bit, least significant bit first (spec 2.1).
/// Reading past the end sets <see cref="EndOfPacket"/> and returns zeros; in
/// audio packets that ends decoding of the packet early, which the spec
/// allows, rather than being an error.
/// </summary>
internal sealed class BitReader(byte[] data)
{
    private readonly byte[] _data = data;
    private long _bit;

    public bool EndOfPacket { get; private set; }

    public uint Read(int count)
    {
        if (count == 0)
        {
            return 0;
        }

        if (_bit + count > (long)_data.Length * 8)
        {
            EndOfPacket = true;
            _bit = (long)_data.Length * 8;
            return 0;
        }

        var value = 0u;
        for (var i = 0; i < count; i++)
        {
            var b = _data[_bit >> 3] >> (int)(_bit & 7) & 1;
            value |= (uint)b << i;
            _bit++;
        }

        return value;
    }

    public bool Flag()
    {
        return Read(1) != 0;
    }

    /// <summary>One bit for walking a Huffman tree; -1 at end of packet.</summary>
    public int Bit()
    {
        if (_bit >= (long)_data.Length * 8)
        {
            EndOfPacket = true;
            return -1;
        }

        var b = _data[_bit >> 3] >> (int)(_bit & 7) & 1;
        _bit++;
        return b;
    }
}
