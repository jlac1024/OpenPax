namespace OpenPax.Data.Compression;

/// <summary>
/// A raw LZMA stream read as a plain stream: five property bytes (the packed
/// settings, then the dictionary size), then the packed data, with no
/// length ahead of it. Decoded only as far as it is read, so a stream whose
/// end is known only to its reader (an Inno Setup index block) is fine.
/// </summary>
internal sealed class LzmaStream : Stream
{
    private readonly LzmaDecoder _decoder;

    public LzmaStream(Stream packed)
    {
        Span<byte> props = stackalloc byte[5];
        packed.ReadExactly(props);
        var dictionary = BitConverter.ToUInt32(props[1..]);
        _decoder = new LzmaDecoder(new OutWindow(dictionary));
        _decoder.SetProperties(props[0]);
        _decoder.ResetState();
        _decoder.Range.Start(packed);
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

    public override int Read(Span<byte> buffer) => _decoder.Decode(buffer);

    public override void Flush()
    {
    }

    public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();

    public override void SetLength(long value) => throw new NotSupportedException();

    public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();
}
