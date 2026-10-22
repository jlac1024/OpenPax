using System.Globalization;
using System.Net.Sockets;
using System.Net.WebSockets;
using OpenPax.Net.Transport;

namespace OpenPax.Net.Lobby;

/// <summary>
/// Where a lobby is: a host and port reached over plain TCP (on a LAN), or a
/// ws:// or wss:// address reached as a WebSocket (a public lobby behind a
/// Cloudflare tunnel or another web proxy). The lobby answers both on one port.
/// </summary>
public sealed record LobbyAddress(string Host, int Port, Uri? WebSocket = null)
{
    /// <summary>How long opening a connection may take before it is given up.</summary>
    public const int Wait = 10000;

    /// <summary>"host", "host:port", "ws://host[:port][/path]" or "wss://host[:port][/path]".</summary>
    public static bool TryParse(string text, out LobbyAddress address)
    {
        address = new LobbyAddress("", 0);
        text = text.Trim();
        if (text.Length == 0)
        {
            return false;
        }

        if (text.Contains("://", StringComparison.Ordinal))
        {
            if (!Uri.TryCreate(text, UriKind.Absolute, out var uri) || (uri.Scheme != "ws" && uri.Scheme != "wss") || uri.Host.Length == 0)
            {
                return false;
            }

            address = new LobbyAddress(uri.Host, uri.Port, uri);
            return true;
        }

        // One colon parts a port off; more than one is an IPv6 address with no port.
        var colon = text.LastIndexOf(':');
        var host = text;
        var port = NetProtocol.DefaultLobbyPort;
        if (colon > 0 && text.IndexOf(':') == colon)
        {
            host = text[..colon];
            if (!int.TryParse(text[(colon + 1)..], NumberStyles.None, CultureInfo.InvariantCulture, out port) || port is < 1 or > 65535)
            {
                return false;
            }
        }

        if (host.Length == 0 || host.Any(char.IsWhiteSpace))
        {
            return false;
        }

        address = new LobbyAddress(host, port);
        return true;
    }

    /// <summary>A stream to the lobby that owns its socket; throws <see cref="IOException"/>, <see cref="SocketException"/> or <see cref="TimeoutException"/> when it cannot open.</summary>
    public Stream Open()
    {
        if (WebSocket is { } uri)
        {
            var socket = new ClientWebSocket();
            socket.Options.KeepAliveInterval = TimeSpan.FromSeconds(20);
            try
            {
                if (!socket.ConnectAsync(uri, CancellationToken.None).Wait(Wait))
                {
                    socket.Dispose();
                    throw new TimeoutException(NetText.NoAnswer);
                }
            }
            catch (AggregateException e)
            {
                socket.Dispose();
                throw new IOException(e.InnerException?.Message ?? e.Message, e.InnerException);
            }

            return new BinaryWebSocketStream(socket);
        }

        var client = new TcpClient { NoDelay = true };
        try
        {
            if (!client.ConnectAsync(Host, Port).Wait(Wait))
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

        return new NetworkStream(client.Client, ownsSocket: true);
    }

    public override string ToString() => WebSocket?.ToString() ?? (Port == NetProtocol.DefaultLobbyPort ? Host : Host + ":" + Port);
}
