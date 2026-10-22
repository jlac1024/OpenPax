namespace OpenPax.Data.Compression;

/// <summary>
/// LZMA's binary range decoder (docs/formats/lzma.md): turns the packed
/// bytes back into bits, each bit weighed by an adaptive probability.
/// </summary>
internal sealed class RangeDecoder
{
    private const int ProbBits = 11;
    private const int MoveBits = 5;
    private const uint TopValue = 1u << 24;

    private Stream _input = Stream.Null;
    private uint _range;
    private uint _code;

    /// <summary>A probability at even odds, what every model starts from.</summary>
    public const ushort Even = 1 << (ProbBits - 1);

    /// <summary>Starts decoding from the stream's current position: five bytes, the first always zero.</summary>
    public void Start(Stream input)
    {
        _input = input;
        _range = 0xFFFFFFFF;
        _code = 0;
        if (Next() != 0)
        {
            throw new InvalidDataException(Strings.LzmaCorrupt);
        }

        for (var i = 0; i < 4; i++)
        {
            _code = (_code << 8) | Next();
        }

        if (_code == _range)
        {
            throw new InvalidDataException(Strings.LzmaCorrupt);
        }
    }

    public int Bit(ref ushort prob)
    {
        var bound = (_range >> ProbBits) * prob;
        int bit;
        if (_code < bound)
        {
            _range = bound;
            prob += (ushort)(((1 << ProbBits) - prob) >> MoveBits);
            bit = 0;
        }
        else
        {
            _range -= bound;
            _code -= bound;
            prob -= (ushort)(prob >> MoveBits);
            bit = 1;
        }

        Normalize();
        return bit;
    }

    /// <summary>Bits at even odds, highest first, with no model behind them.</summary>
    public uint Direct(int count)
    {
        uint result = 0;
        for (var i = 0; i < count; i++)
        {
            _range >>= 1;
            uint bit = _code >= _range ? 1u : 0u;
            _code -= _range & (0u - bit);
            result = (result << 1) | bit;
            Normalize();
        }

        return result;
    }

    /// <summary>
    /// A symbol of <paramref name="bits"/> bits read down a tree of
    /// probabilities, highest bit first. The tree's nodes are
    /// probs[offset + 1] up to probs[offset + 2^bits - 1].
    /// </summary>
    public uint Tree(ushort[] probs, int offset, int bits)
    {
        uint m = 1;
        for (var i = 0; i < bits; i++)
        {
            m = (m << 1) | (uint)Bit(ref probs[offset + (int)m]);
        }

        return m - (1u << bits);
    }

    /// <summary>The same, lowest bit first.</summary>
    public uint ReverseTree(ushort[] probs, int offset, int bits)
    {
        uint m = 1;
        uint symbol = 0;
        for (var i = 0; i < bits; i++)
        {
            var bit = (uint)Bit(ref probs[offset + (int)m]);
            m = (m << 1) | bit;
            symbol |= bit << i;
        }

        return symbol;
    }

    private void Normalize()
    {
        if (_range < TopValue)
        {
            _range <<= 8;
            _code = (_code << 8) | Next();
        }
    }

    private uint Next()
    {
        var b = _input.ReadByte();
        if (b < 0)
        {
            throw new InvalidDataException(Strings.LzmaTruncated);
        }

        return (uint)b;
    }
}
