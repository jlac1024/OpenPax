using System.Net.WebSockets;

namespace OpenPax.Net.Transport;

/// <summary>
/// A WebSocket as a byte stream, so the game's own framed messages run over
/// it unchanged (through a Cloudflare tunnel, which carries web traffic, not
/// plain TCP). Writes go out as binary messages; reads return what arrives,
/// message boundaries ignored. Blocking, as the connections' threads expect,
/// and with a read timeout (which the runtime's own WebSocketStream lacks)
/// so the lobby can drop an arrival that never says who it is.
/// </summary>
public sealed class BinaryWebSocketStream(WebSocket socket) : Stream
{
    private readonly Lock _writing = new();
    private bool _disposed;

    public override bool CanRead => true;

    public override bool CanWrite => true;

    public override bool CanSeek => false;

    public override bool CanTimeout => true;

    /// <summary>How long a read waits, in milliseconds; a read that runs out breaks the socket, as a timed out socket read does.</summary>
    public override int ReadTimeout { get; set; } = Timeout.Infinite;

    public override long Length => throw new NotSupportedException();

    public override long Position
    {
        get => throw new NotSupportedException();
        set => throw new NotSupportedException();
    }

    public override int Read(byte[] buffer, int offset, int count) => Read(buffer.AsSpan(offset, count));

    public override int Read(Span<byte> buffer)
    {
        if (buffer.Length == 0)
        {
            return 0;
        }

        var rented = new byte[buffer.Length];
        while (true)
        {
            WebSocketReceiveResult result;
            try
            {
                using var wait = ReadTimeout == Timeout.Infinite ? new CancellationTokenSource() : new CancellationTokenSource(ReadTimeout);
                result = socket.ReceiveAsync(rented, wait.Token).GetAwaiter().GetResult();
            }
            catch (Exception e) when (e is WebSocketException or ObjectDisposedException or OperationCanceledException)
            {
                throw new IOException(e.Message, e);
            }

            if (result.MessageType == WebSocketMessageType.Close)
            {
                return 0;
            }

            // A zero-length piece of a message: wait for more.
            if (result.Count > 0)
            {
                rented.AsSpan(0, result.Count).CopyTo(buffer);
                return result.Count;
            }
        }
    }

    public override void Write(byte[] buffer, int offset, int count) => Write(buffer.AsSpan(offset, count));

    public override void Write(ReadOnlySpan<byte> buffer)
    {
        var copy = buffer.ToArray();
        lock (_writing)
        {
            try
            {
                socket.SendAsync(copy, WebSocketMessageType.Binary, true, CancellationToken.None).GetAwaiter().GetResult();
            }
            catch (Exception e) when (e is WebSocketException or ObjectDisposedException or OperationCanceledException)
            {
                throw new IOException(e.Message, e);
            }
        }
    }

    public override void Flush()
    {
    }

    public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();

    public override void SetLength(long value) => throw new NotSupportedException();

    protected override void Dispose(bool disposing)
    {
        if (disposing && !_disposed)
        {
            _disposed = true;
            try
            {
                if (socket.State == WebSocketState.Open)
                {
                    socket.CloseOutputAsync(WebSocketCloseStatus.NormalClosure, null, CancellationToken.None).Wait(1000);
                }
            }
            catch (Exception e) when (e is WebSocketException or ObjectDisposedException or AggregateException or OperationCanceledException)
            {
                // Closing an already broken socket.
            }

            socket.Dispose();
        }

        base.Dispose(disposing);
    }
}
