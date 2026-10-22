namespace OpenPax.Data.Compression;

/// <summary>
/// An LZMA2 stream read as a plain stream (docs/formats/lzma.md): one byte
/// for the dictionary size, then chunks, each stored or LZMA packed, each
/// saying how big it is, up to a zero byte.
/// </summary>
internal sealed class Lzma2Stream : Stream
{
    private readonly Stream _packed;
    private readonly OutWindow _window;
    private readonly LzmaDecoder _decoder;
    private long _chunkLeft;
    private bool _chunkStored;
    private bool _haveProperties;
    private bool _ended;
    private long _chunkEnd = -1;

    /// <param name="expected">How many bytes will be read at most, if known, so the window need not be bigger.</param>
    public Lzma2Stream(Stream packed, long? expected = null)
    {
        _packed = packed;
        var bits = packed.ReadByte();
        if (bits is < 0 or > 40)
        {
            throw new InvalidDataException(Strings.LzmaCorrupt);
        }

        var dictionary = bits == 40 ? 0xFFFFFFFFL : (2L | (bits & 1)) << ((bits / 2) + 11);
        _window = new OutWindow(dictionary, expected);
        _decoder = new LzmaDecoder(_window);
    }

    public override bool CanRead => true;

    public override bool CanSeek => false;

    public override bool CanWrite => false;

    public override long Length => throw new NotSupportedException();

    public override long Position
    {
        get => throw new NotSupportedException();
        set => throw new NotSupportedException();
    }

    public override int Read(byte[] buffer, int offset, int count) => Read(buffer.AsSpan(offset, count));

    public override int Read(Span<byte> buffer)
    {
        var done = 0;
        while (done < buffer.Length && !_ended)
        {
            if (_chunkLeft == 0 && !NextChunk())
            {
                break;
            }

            var want = (int)Math.Min(buffer.Length - done, _chunkLeft);
            var target = buffer.Slice(done, want);
            int got;
            if (_chunkStored)
            {
                got = _packed.Read(target);
                if (got == 0)
                {
                    throw new InvalidDataException(Strings.LzmaTruncated);
                }

                foreach (var b in target[..got])
                {
                    _window.Put(b);
                }
            }
            else
            {
                got = _decoder.Decode(target);
                if (got < want)
                {
                    // LZMA2 chunks carry no end mark: the chunk's size ends it.
                    throw new InvalidDataException(Strings.LzmaCorrupt);
                }
            }

            done += got;
            _chunkLeft -= got;
        }

        return done;
    }

    // Reads a chunk's header: false at the end mark.
    private bool NextChunk()
    {
        if (_decoder.Pending)
        {
            throw new InvalidDataException(Strings.LzmaCorrupt);
        }

        // The range decoder reads its chunk exactly, but going by the size
        // the chunk gave keeps one wrong byte count from running on.
        if (_chunkEnd >= 0 && _packed.CanSeek)
        {
            _packed.Position = _chunkEnd;
        }

        _chunkEnd = -1;
        var control = Byte();
        if (control == 0)
        {
            _ended = true;
            return false;
        }

        if (control is 1 or 2)
        {
            if (control == 1)
            {
                _window.Reset();
            }

            _chunkStored = true;
            _chunkLeft = BigEndian16() + 1;
            return true;
        }

        if (control < 0x80)
        {
            throw new InvalidDataException(Strings.LzmaCorrupt);
        }

        _chunkStored = false;
        _chunkLeft = ((long)(control & 0x1F) << 16) + BigEndian16() + 1;
        var packedSize = BigEndian16() + 1;
        var reset = (control >> 5) & 3;
        if (reset == 3)
        {
            _window.Reset();
        }

        if (reset >= 2)
        {
            _decoder.SetProperties(Byte());
            _haveProperties = true;
        }
        else if (!_haveProperties)
        {
            throw new InvalidDataException(Strings.LzmaCorrupt);
        }

        if (reset >= 1)
        {
            _decoder.ResetState();
        }

        if (_packed.CanSeek)
        {
            _chunkEnd = _packed.Position + packedSize;
        }

        _decoder.Range.Start(_packed);
        return true;
    }

    private int Byte()
    {
        var b = _packed.ReadByte();
        return b >= 0 ? b : throw new InvalidDataException(Strings.LzmaTruncated);
    }

    private int BigEndian16() => (Byte() << 8) | Byte();

    public override void Flush()
    {
    }

    public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();

    public override void SetLength(long value) => throw new NotSupportedException();

    public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();
}
