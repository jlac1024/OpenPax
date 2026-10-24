using System.Net.Sockets;
using System.Text;

namespace OpenPax.Host.Update.DBus;

/// <summary>
/// A connection to the D-Bus session bus, enough for the Flatpak portal
/// (FlatpakPortal): method calls with string, object path and empty
/// dictionary arguments, their replies, and signals. Our own small client
/// (docs/updates.md), as the portal ties an update monitor to the
/// connection that made it, and the gdbus tool makes one per call.
/// </summary>
internal sealed class DBusConnection : IDisposable
{
    private readonly Socket _socket;
    private readonly NetworkStream _stream;
    private readonly Queue<DBusMessage> _signals = new();
    private uint _serial;

    private DBusConnection(Socket socket)
    {
        _socket = socket;
        _stream = new NetworkStream(socket, ownsSocket: false);
    }

    /// <summary>Opens the session bus (DBUS_SESSION_BUS_ADDRESS, a unix: path or abstract name) and says hello.</summary>
    public static async Task<DBusConnection> OpenSessionAsync(CancellationToken cancel)
    {
        var address = Environment.GetEnvironmentVariable("DBUS_SESSION_BUS_ADDRESS") ?? throw new IOException("no D-Bus session bus (DBUS_SESSION_BUS_ADDRESS is not set)");
        var socket = new Socket(AddressFamily.Unix, SocketType.Stream, ProtocolType.Unspecified);
        try
        {
            await socket.ConnectAsync(EndPoint(address), cancel).ConfigureAwait(false);
            var connection = new DBusConnection(socket);
            await connection.AuthenticateAsync(cancel).ConfigureAwait(false);
            await connection.CallAsync("org.freedesktop.DBus", "/org/freedesktop/DBus", "org.freedesktop.DBus", "Hello", cancel).ConfigureAwait(false);
            return connection;
        }
        catch
        {
            socket.Dispose();
            throw;
        }
    }

    // unix:path=/run/user/1000/bus or unix:abstract=/tmp/dbus-x,guid=... (the first unix address of a list).
    private static UnixDomainSocketEndPoint EndPoint(string address)
    {
        foreach (var entry in address.Split(';'))
        {
            if (!entry.StartsWith("unix:", StringComparison.Ordinal))
            {
                continue;
            }

            var keys = entry[5..].Split(',').Select(p => p.Split('=', 2)).Where(p => p.Length == 2).ToDictionary(p => p[0], p => Uri.UnescapeDataString(p[1]), StringComparer.Ordinal);
            if (keys.TryGetValue("path", out var path))
            {
                return new UnixDomainSocketEndPoint(path);
            }

            if (keys.TryGetValue("abstract", out var name))
            {
                return new UnixDomainSocketEndPoint("\0" + name);
            }
        }

        throw new IOException("no unix D-Bus address in " + address);
    }

    // SASL EXTERNAL: the bus checks this process's user id against the socket's.
    private async Task AuthenticateAsync(CancellationToken cancel)
    {
        var uid = File.ReadLines("/proc/self/status").First(l => l.StartsWith("Uid:", StringComparison.Ordinal)).Split((char[])['\t', ' '], StringSplitOptions.RemoveEmptyEntries)[1];
        var hex = Convert.ToHexString(Encoding.ASCII.GetBytes(uid)).ToLowerInvariant();
        await _stream.WriteAsync(Encoding.ASCII.GetBytes("\0AUTH EXTERNAL " + hex + "\r\n"), cancel).ConfigureAwait(false);
        var reply = await ReadLineAsync(cancel).ConfigureAwait(false);
        if (!reply.StartsWith("OK", StringComparison.Ordinal))
        {
            throw new IOException("D-Bus did not accept this user: " + reply);
        }

        await _stream.WriteAsync(Encoding.ASCII.GetBytes("BEGIN\r\n"), cancel).ConfigureAwait(false);
    }

    private async Task<string> ReadLineAsync(CancellationToken cancel)
    {
        var line = new StringBuilder();
        var one = new byte[1];
        while (await _stream.ReadAsync(one, cancel).ConfigureAwait(false) == 1)
        {
            if (one[0] == '\n')
            {
                return line.ToString().TrimEnd('\r');
            }

            line.Append((char)one[0]);
        }

        throw new IOException("the D-Bus connection closed");
    }

    /// <summary>
    /// Calls a method and waits for its reply's values; a D-Bus error becomes
    /// an IOException with its name and message. Arguments are strings
    /// (signature s), or null for an empty a{sv} options dictionary.
    /// </summary>
    public async Task<List<object>> CallAsync(string destination, string path, string iface, string member, CancellationToken cancel, params string?[] args)
    {
        var serial = ++_serial;
        await _stream.WriteAsync(Call(serial, destination, path, iface, member, args), cancel).ConfigureAwait(false);
        while (true)
        {
            var message = await ReceiveAsync(cancel).ConfigureAwait(false);
            if (message.Kind == DBusMessage.Kinds.Signal)
            {
                _signals.Enqueue(message);
            }
            else if (message.ReplySerial == serial)
            {
                return message.Kind == DBusMessage.Kinds.Error
                    ? throw new IOException(message.ErrorName + (message.Body.FirstOrDefault() is string text ? ": " + text : ""))
                    : message.Body;
            }
        }
    }

    /// <summary>The next signal: one that came while waiting for a reply, or the next to arrive.</summary>
    public async Task<DBusMessage> NextSignalAsync(CancellationToken cancel)
    {
        if (_signals.TryDequeue(out var queued))
        {
            return queued;
        }

        while (true)
        {
            var message = await ReceiveAsync(cancel).ConfigureAwait(false);
            if (message.Kind == DBusMessage.Kinds.Signal)
            {
                return message;
            }
        }
    }

    internal static byte[] Call(uint serial, string destination, string path, string iface, string member, string?[] args)
    {
        var body = new DBusWriter();
        var signature = new StringBuilder();
        foreach (var arg in args)
        {
            if (arg is null)
            {
                body.EmptyDictionary();
                signature.Append("a{sv}");
            }
            else
            {
                body.String(arg);
                signature.Append('s');
            }
        }

        var bytes = body.ToArray();
        var header = new DBusWriter();
        header.Byte((byte)'l');
        header.Byte((byte)DBusMessage.Kinds.MethodCall);
        header.Byte(0);
        header.Byte(1);
        header.UInt32((uint)bytes.Length);
        header.UInt32(serial);
        var lengthAt = header.Length;
        header.UInt32(0);
        var fieldsStart = header.Length;
        void Field(byte code, char type, string value)
        {
            header.Pad(8);
            header.Byte(code);
            header.Signature(type.ToString());
            if (type == 'g')
            {
                header.Signature(value);
            }
            else
            {
                header.String(value);
            }
        }

        Field(1, 'o', path);
        Field(2, 's', iface);
        Field(3, 's', member);
        Field(6, 's', destination);
        if (signature.Length > 0)
        {
            Field(8, 'g', signature.ToString());
        }

        header.SetUInt32(lengthAt, (uint)(header.Length - fieldsStart));
        header.Pad(8);
        return [.. header.ToArray(), .. bytes];
    }

    private async Task<DBusMessage> ReceiveAsync(CancellationToken cancel)
    {
        // The fixed part: endianness, type, flags, version, body length, serial, header field array length.
        var fixedPart = new byte[16];
        await _stream.ReadExactlyAsync(fixedPart, cancel).ConfigureAwait(false);
        var bodyLength = BitConverter.ToInt32(fixedPart, 4);
        var fieldsLength = BitConverter.ToInt32(fixedPart, 12);
        var headerEnd = 16 + fieldsLength;
        var padded = (headerEnd + 7) / 8 * 8;
        var whole = new byte[padded + bodyLength];
        Array.Copy(fixedPart, whole, 16);
        await _stream.ReadExactlyAsync(whole.AsMemory(16), cancel).ConfigureAwait(false);
        return DBusMessage.Read(whole);
    }

    public void Dispose()
    {
        _stream.Dispose();
        _socket.Dispose();
    }
}
