using System.Buffers.Binary;
using System.Text;

namespace OpenPax.Net.Lan;

/// <summary>
/// The two datagrams of LAN discovery (docs/netcode/lan-discovery.md): a
/// game broadcasts a question to the lobby port over UDP, and each lobby on
/// the LAN answers with its name, TCP port, protocol version and players.
/// </summary>
public static class LanDiscovery
{
    private const int MaxName = 64;

    public static ReadOnlySpan<byte> Question => "HGLOBBY?"u8;

    private static ReadOnlySpan<byte> AnswerMark => "HGLOBBY!"u8;

    public static byte[] Answer(LanLobby lobby)
    {
        var name = Encoding.UTF8.GetBytes(lobby.Name.Length > MaxName ? lobby.Name[..MaxName] : lobby.Name);
        var bytes = new byte[AnswerMark.Length + 6 + name.Length];
        AnswerMark.CopyTo(bytes);
        var at = AnswerMark.Length;
        BinaryPrimitives.WriteUInt16BigEndian(bytes.AsSpan(at), (ushort)lobby.Version);
        BinaryPrimitives.WriteUInt16BigEndian(bytes.AsSpan(at + 2), (ushort)lobby.Port);
        BinaryPrimitives.WriteUInt16BigEndian(bytes.AsSpan(at + 4), (ushort)Math.Min(lobby.Players, ushort.MaxValue));
        name.CopyTo(bytes, at + 6);
        return bytes;
    }

    /// <summary>An answer read back, with the host it came from; null for anything else.</summary>
    public static LanLobby? Read(ReadOnlySpan<byte> datagram, string host)
    {
        if (datagram.Length < AnswerMark.Length + 6 || !datagram[..AnswerMark.Length].SequenceEqual(AnswerMark))
        {
            return null;
        }

        var body = datagram[AnswerMark.Length..];
        var name = body[6..];
        return new LanLobby(
            Encoding.UTF8.GetString(name[..Math.Min(name.Length, MaxName * 4)]),
            host,
            BinaryPrimitives.ReadUInt16BigEndian(body[2..]),
            BinaryPrimitives.ReadUInt16BigEndian(body),
            BinaryPrimitives.ReadUInt16BigEndian(body[4..]));
    }
}
