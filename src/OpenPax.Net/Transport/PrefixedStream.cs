namespace OpenPax.Net.Transport;

/// <summary>A stream with some bytes already read off it put back in front, so whatever reads next sees them first.</summary>
public sealed class PrefixedStream(byte[] prefix, Stream inner) : Stream
{
    private int _taken;

    public override bool CanRead => true;

    public override bool CanWrite => inner.CanWrite;

    public override bool CanSeek => false;

    public override long Length => throw new NotSupportedException();

    public override long Position
    {
        get => throw new NotSupportedException();
        set => throw new NotSupportedException();
    }

    public override int ReadTimeout
    {
        get => inner.ReadTimeout;
        set => inner.ReadTimeout = value;
    }

    public override bool CanTimeout => inner.CanTimeout;

    public override int Read(byte[] buffer, int offset, int count) => Read(buffer.AsSpan(offset, count));

    public override int Read(Span<byte> buffer)
    {
        if (_taken < prefix.Length)
        {
            var n = Math.Min(buffer.Length, prefix.Length - _taken);
            prefix.AsSpan(_taken, n).CopyTo(buffer);
            _taken += n;
            return n;
        }

        return inner.Read(buffer);
    }

    public override void Write(byte[] buffer, int offset, int count) => inner.Write(buffer, offset, count);

    public override void Write(ReadOnlySpan<byte> buffer) => inner.Write(buffer);

    public override void Flush() => inner.Flush();

    public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();

    public override void SetLength(long value) => throw new NotSupportedException();

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            inner.Dispose();
        }

        base.Dispose(disposing);
    }
}
