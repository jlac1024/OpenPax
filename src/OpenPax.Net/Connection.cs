using System.Collections.Concurrent;
using System.Net.Sockets;
using OpenPax.Net.Messages;
using OpenPax.Net.Wire;

namespace OpenPax.Net;

/// <summary>
/// One end of a connection: messages are sent at once, and a background
/// thread reads arriving ones into a queue the game loop drains each frame,
/// so the loop never waits on the network.
/// </summary>
public sealed class Connection : IDisposable
{
    private readonly Stream _stream;
    private readonly TcpClient? _client;
    private readonly ConcurrentQueue<NetMessage> _arrived = new();
    private readonly Lock _sending = new();
    private volatile bool _closed;

    /// <param name="first">A message already read off the stream before this connection took it over.</param>
    public Connection(Stream stream, TcpClient? client = null, NetMessage? first = null)
    {
        _stream = stream;
        _client = client;
        if (first is not null)
        {
            _arrived.Enqueue(first);
        }

        var reader = new Thread(ReadLoop) { IsBackground = true, Name = "OpenPax connection" };
        reader.Start();
    }

    /// <summary>The connection ended, from either end or by an error.</summary>
    public bool Closed => _closed;

    /// <summary>Why it ended, when an error ended it.</summary>
    public string? Error { get; private set; }

    /// <summary>Connects, giving up after the wait; throws <see cref="SocketException"/> or <see cref="TimeoutException"/> when it cannot.</summary>
    public static Connection Connect(string host, int port, int wait = 5000)
    {
        var client = new TcpClient { NoDelay = true };
        try
        {
            if (!client.ConnectAsync(host, port).Wait(wait))
            {
                throw new TimeoutException(NetText.NoAnswer);
            }
        }
        catch (AggregateException e) when (e.InnerException is SocketException inner)
        {
            client.Dispose();
            throw inner;
        }
        catch (TimeoutException)
        {
            client.Dispose();
            throw;
        }

        return new Connection(client.GetStream(), client);
    }

    public static Connection Accept(TcpClient client)
    {
        client.NoDelay = true;
        return new Connection(client.GetStream(), client);
    }

    public void Send(NetMessage message)
    {
        if (_closed)
        {
            return;
        }

        var (type, body) = MessageCodec.Encode(message);
        try
        {
            lock (_sending)
            {
                Frames.Write(_stream, type, body);
            }
        }
        catch (Exception e) when (e is IOException or ObjectDisposedException)
        {
            Close(e.Message);
        }
    }

    public bool TryReceive(out NetMessage message)
    {
        if (_arrived.TryDequeue(out var m))
        {
            message = m;
            return true;
        }

        message = null!; // Only read when true is returned.
        return false;
    }

    public void Dispose() => Close(null);

    private void ReadLoop()
    {
        try
        {
            while (!_closed && Frames.Read(_stream) is { } frame)
            {
                _arrived.Enqueue(MessageCodec.Decode(frame.Type, frame.Body));
            }

            Close(null);
        }
        catch (Exception e) when (e is IOException or ObjectDisposedException or InvalidDataException or EndOfStreamException)
        {
            Close(e.Message);
        }
    }

    private void Close(string? error)
    {
        if (_closed)
        {
            return;
        }

        Error ??= error;
        _closed = true;
        _stream.Dispose();
        _client?.Dispose();
    }
}
