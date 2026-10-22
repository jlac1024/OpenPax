namespace OpenPax.Data.Compression;

/// <summary>
/// Decodes LZMA (docs/formats/lzma.md), as GOG's Inno Setup installer packs
/// its index and files. Written from the format's description; the output
/// can stop anywhere, even inside a match, and carry on at the next call.
/// </summary>
internal sealed class LzmaDecoder
{
    private const int States = 12;
    private const int PosStatesMax = 16;
    private const int LenStates = 4;
    private const int PosSlotBits = 6;
    private const int EndPosModelIndex = 14;
    private const int FullDistances = 1 << (EndPosModelIndex / 2);
    private const int AlignBits = 4;
    private const int MatchMinLength = 2;

    private readonly OutWindow _window;
    private readonly LengthDecoder _lengths = new();
    private readonly LengthDecoder _repLengths = new();
    private readonly ushort[] _isMatch = new ushort[States * PosStatesMax];
    private readonly ushort[] _isRep = new ushort[States];
    private readonly ushort[] _isRepG0 = new ushort[States];
    private readonly ushort[] _isRepG1 = new ushort[States];
    private readonly ushort[] _isRepG2 = new ushort[States];
    private readonly ushort[] _isRep0Long = new ushort[States * PosStatesMax];
    private readonly ushort[] _posSlots = new ushort[LenStates << PosSlotBits];
    private readonly ushort[] _posSpecial = new ushort[FullDistances - EndPosModelIndex];
    private readonly ushort[] _align = new ushort[1 << AlignBits];
    private ushort[] _literals = [];
    private int _lc;
    private int _lp;
    private int _pb;
    private int _state;
    private uint _rep0;
    private uint _rep1;
    private uint _rep2;
    private uint _rep3;
    private int _pending;

    public LzmaDecoder(OutWindow window)
    {
        _window = window;
    }

    public RangeDecoder Range { get; } = new();

    /// <summary>Set when the stream's end mark was read.</summary>
    public bool Ended { get; private set; }

    /// <summary>Whether a match is still being copied out.</summary>
    public bool Pending => _pending > 0;

    /// <summary>The literal and position settings, packed in one byte as (pb * 5 + lp) * 9 + lc.</summary>
    public void SetProperties(int packed)
    {
        if (packed >= 9 * 5 * 5)
        {
            throw new InvalidDataException(Strings.LzmaCorrupt);
        }

        _lc = packed % 9;
        _lp = packed / 9 % 5;
        _pb = packed / 45;
        _literals = new ushort[0x300 << (_lc + _lp)];
    }

    /// <summary>All the models back to even odds, as at a stream's start.</summary>
    public void ResetState()
    {
        Array.Fill(_isMatch, RangeDecoder.Even);
        Array.Fill(_isRep, RangeDecoder.Even);
        Array.Fill(_isRepG0, RangeDecoder.Even);
        Array.Fill(_isRepG1, RangeDecoder.Even);
        Array.Fill(_isRepG2, RangeDecoder.Even);
        Array.Fill(_isRep0Long, RangeDecoder.Even);
        Array.Fill(_posSlots, RangeDecoder.Even);
        Array.Fill(_posSpecial, RangeDecoder.Even);
        Array.Fill(_align, RangeDecoder.Even);
        Array.Fill(_literals, RangeDecoder.Even);
        _lengths.Reset();
        _repLengths.Reset();
        _state = 0;
        _rep0 = _rep1 = _rep2 = _rep3 = 0;
        _pending = 0;
        Ended = false;
    }

    /// <summary>
    /// Fills <paramref name="output"/> as far as the stream goes; fewer
    /// bytes only at the end mark. The bytes also go into the window.
    /// </summary>
    public int Decode(Span<byte> output)
    {
        var done = 0;
        while (done < output.Length)
        {
            if (_pending > 0)
            {
                var b = _window.Back(_rep0 + 1);
                _window.Put(b);
                output[done++] = b;
                _pending--;
                continue;
            }

            if (Ended)
            {
                break;
            }

            if (Step() is { } literal)
            {
                output[done++] = literal;
            }
        }

        return done;
    }

    // One packet: a literal (returned), or a match whose length is left in _pending.
    private byte? Step()
    {
        var posState = (int)(_window.Total & ((1 << _pb) - 1));
        if (Range.Bit(ref _isMatch[(_state * PosStatesMax) + posState]) == 0)
        {
            var literal = Literal();
            _window.Put(literal);
            _state = _state < 4 ? 0 : _state < 10 ? _state - 3 : _state - 6;
            return literal;
        }

        uint length;
        if (Range.Bit(ref _isRep[_state]) == 1)
        {
            if (_window.IsEmpty)
            {
                throw new InvalidDataException(Strings.LzmaCorrupt);
            }

            if (Range.Bit(ref _isRepG0[_state]) == 0)
            {
                if (Range.Bit(ref _isRep0Long[(_state * PosStatesMax) + posState]) == 0)
                {
                    // A single byte from the last distance.
                    _state = _state < 7 ? 9 : 11;
                    var b = _window.Back(_rep0 + 1);
                    _window.Put(b);
                    return b;
                }
            }
            else
            {
                uint distance;
                if (Range.Bit(ref _isRepG1[_state]) == 0)
                {
                    distance = _rep1;
                }
                else
                {
                    if (Range.Bit(ref _isRepG2[_state]) == 0)
                    {
                        distance = _rep2;
                    }
                    else
                    {
                        distance = _rep3;
                        _rep3 = _rep2;
                    }

                    _rep2 = _rep1;
                }

                _rep1 = _rep0;
                _rep0 = distance;
            }

            length = _repLengths.Decode(Range, posState);
            _state = _state < 7 ? 8 : 11;
        }
        else
        {
            _rep3 = _rep2;
            _rep2 = _rep1;
            _rep1 = _rep0;
            length = _lengths.Decode(Range, posState);
            _state = _state < 7 ? 7 : 10;
            _rep0 = Distance(length);
            if (_rep0 == 0xFFFFFFFF)
            {
                Ended = true;
                return null;
            }

            if (!_window.Holds(_rep0 + 1))
            {
                throw new InvalidDataException(Strings.LzmaCorrupt);
            }
        }

        _pending = (int)length + MatchMinLength;
        return null;
    }

    private byte Literal()
    {
        var previous = _window.IsEmpty ? 0 : _window.Back(1);
        var litState = ((int)(_window.Total & ((1 << _lp) - 1)) << _lc) + (previous >> (8 - _lc));
        var offset = 0x300 * litState;
        uint symbol = 1;
        if (_state >= 7)
        {
            // After a match, the byte at the last distance guides the first bits.
            uint matchByte = _window.Back(_rep0 + 1);
            while (symbol < 0x100)
            {
                var matchBit = (matchByte >> 7) & 1;
                matchByte <<= 1;
                var bit = (uint)Range.Bit(ref _literals[offset + (int)(((1 + matchBit) << 8) + symbol)]);
                symbol = (symbol << 1) | bit;
                if (matchBit != bit)
                {
                    break;
                }
            }
        }

        while (symbol < 0x100)
        {
            symbol = (symbol << 1) | (uint)Range.Bit(ref _literals[offset + (int)symbol]);
        }

        return (byte)symbol;
    }

    // The distance less one; all ones is the end mark.
    private uint Distance(uint length)
    {
        var lenState = (int)Math.Min(length, LenStates - 1);
        var slot = Range.Tree(_posSlots, lenState << PosSlotBits, PosSlotBits);
        if (slot < 4)
        {
            return slot;
        }

        var directBits = (int)(slot >> 1) - 1;
        var distance = (2 | (slot & 1)) << directBits;
        if (slot < EndPosModelIndex)
        {
            return distance + Range.ReverseTree(_posSpecial, (int)(distance - slot) - 1, directBits);
        }

        distance += Range.Direct(directBits - AlignBits) << AlignBits;
        return distance + Range.ReverseTree(_align, 0, AlignBits);
    }
}
