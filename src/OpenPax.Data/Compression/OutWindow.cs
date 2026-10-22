namespace OpenPax.Data.Compression;

/// <summary>
/// The bytes LZMA has put out, kept for as far back as matches may reach:
/// up to the dictionary size, grown as needed so a small stream with a big
/// dictionary does not hold the whole dictionary's memory.
/// </summary>
internal sealed class OutWindow
{
    private readonly int _limit;
    private byte[] _buffer;
    private int _pos;
    private bool _full;

    public OutWindow(long dictionarySize, long? expected = null)
    {
        var limit = Math.Max(dictionarySize, 4096);
        if (expected is { } size)
        {
            limit = Math.Min(limit, Math.Max(size, 1));
        }

        _limit = (int)Math.Min(limit, Array.MaxLength);
        _buffer = new byte[Math.Min(_limit, 1 << 16)];
    }

    /// <summary>How many bytes have ever been put out.</summary>
    public long Total { get; private set; }

    public bool IsEmpty => Total == 0;

    /// <summary>Forgets everything (an LZMA2 dictionary reset).</summary>
    public void Reset()
    {
        _pos = 0;
        _full = false;
        Total = 0;
    }

    public void Put(byte b)
    {
        if (_pos == _buffer.Length)
        {
            if (_buffer.Length < _limit)
            {
                Array.Resize(ref _buffer, (int)Math.Min((long)_buffer.Length * 2, _limit));
            }
            else
            {
                _pos = 0;
                _full = true;
            }
        }

        _buffer[_pos++] = b;
        Total++;
    }

    /// <summary>Whether <paramref name="distance"/> bytes back (1 is the last one) is still held.</summary>
    public bool Holds(uint distance) =>
        distance >= 1 && distance <= Total && distance <= (_full ? _buffer.Length : _pos);

    /// <summary>The byte <paramref name="distance"/> back, 1 being the last put out.</summary>
    public byte Back(uint distance)
    {
        var at = _pos - (long)distance;
        if (at < 0)
        {
            at += _buffer.Length;
        }

        return _buffer[at];
    }
}
