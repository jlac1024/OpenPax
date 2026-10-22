namespace OpenPax.Data.Compression;

/// <summary>LZMA's match length model: 2 to 273, in three bands (docs/formats/lzma.md).</summary>
internal sealed class LengthDecoder
{
    private const int LowBits = 3;
    private const int MidBits = 3;
    private const int HighBits = 8;
    private const int PosStates = 16;

    private readonly ushort[] _low = new ushort[PosStates << LowBits];
    private readonly ushort[] _mid = new ushort[PosStates << MidBits];
    private readonly ushort[] _high = new ushort[1 << HighBits];
    private ushort _choice;
    private ushort _choice2;

    public void Reset()
    {
        _choice = RangeDecoder.Even;
        _choice2 = RangeDecoder.Even;
        Array.Fill(_low, RangeDecoder.Even);
        Array.Fill(_mid, RangeDecoder.Even);
        Array.Fill(_high, RangeDecoder.Even);
    }

    /// <summary>The length less the shortest match (2).</summary>
    public uint Decode(RangeDecoder rc, int posState)
    {
        if (rc.Bit(ref _choice) == 0)
        {
            return rc.Tree(_low, posState << LowBits, LowBits);
        }

        if (rc.Bit(ref _choice2) == 0)
        {
            return (1 << LowBits) + rc.Tree(_mid, posState << MidBits, MidBits);
        }

        return (1 << LowBits) + (1 << MidBits) + rc.Tree(_high, 0, HighBits);
    }
}
