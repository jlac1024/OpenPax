using System.Net.WebSockets;
using System.Security.Cryptography;
using System.Text;

namespace OpenPax.Net.Transport;

/// <summary>
/// The server side of a WebSocket opening (RFC 6455): the HTTP request read,
/// answered with 101 Switching Protocols, and the socket made over the same
/// stream. A request that is not an upgrade gets a short plain answer, so a
/// browser or a tunnel's health check sees what this is.
/// </summary>
public static class WebSocketHandshake
{
    private const string Magic = "258EAFA5-E914-47DA-95CA-C5AB0DC85B11";
    private const int MaxRequest = 16 * 1024;

    /// <summary>The socket, or null when the request was not a WebSocket opening (it has then been answered).</summary>
    /// <param name="prefix">What was already read off the stream (the start of "GET ").</param>
    public static WebSocket? Accept(Stream stream, byte[] prefix)
    {
        var request = new List<byte>(prefix);
        var one = new byte[1];
        while (!EndsWithBlankLine(request))
        {
            if (request.Count > MaxRequest || stream.Read(one, 0, 1) != 1)
            {
                return null;
            }

            request.Add(one[0]);
        }

        var lines = Encoding.ASCII.GetString([.. request]).Split("\r\n");
        var headers = lines.Skip(1)
            .Select(l => l.Split(':', 2))
            .Where(p => p.Length == 2)
            .GroupBy(p => p[0].Trim(), StringComparer.OrdinalIgnoreCase)
            .ToDictionary(g => g.Key, g => g.First()[1].Trim(), StringComparer.OrdinalIgnoreCase);
        if (!headers.TryGetValue("Sec-WebSocket-Key", out var key)
            || !headers.TryGetValue("Upgrade", out var upgrade) || !upgrade.Equals("websocket", StringComparison.OrdinalIgnoreCase))
        {
            Answer(stream, "HTTP/1.1 200 OK\r\nContent-Type: text/plain\r\nConnection: close\r\nContent-Length: 18\r\n\r\nOpenPax lobby\r\n");
            return null;
        }

        var accept = Convert.ToBase64String(SHA1.HashData(Encoding.ASCII.GetBytes(key + Magic)));
        Answer(stream, "HTTP/1.1 101 Switching Protocols\r\nUpgrade: websocket\r\nConnection: Upgrade\r\nSec-WebSocket-Accept: " + accept + "\r\n\r\n");
        return WebSocket.CreateFromStream(stream, new WebSocketCreationOptions { IsServer = true, KeepAliveInterval = TimeSpan.FromSeconds(20) });
    }

    private static bool EndsWithBlankLine(List<byte> b) =>
        b.Count >= 4 && b[^4] == '\r' && b[^3] == '\n' && b[^2] == '\r' && b[^1] == '\n';

    private static void Answer(Stream stream, string text)
    {
        var bytes = Encoding.ASCII.GetBytes(text);
        stream.Write(bytes);
        stream.Flush();
    }
}
