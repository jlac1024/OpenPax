using System.Text;
using OpenPax.Net.Messages;
using OpenPax.Net.State;
using OpenPax.Sim;

namespace OpenPax.Net.Wire;

/// <summary>Messages to and from bytes: little endian numbers, UTF-8 strings with a length, byte arrays with a length.</summary>
public static class MessageCodec
{
    public static (MessageType Type, byte[] Body) Encode(NetMessage message)
    {
        using var stream = new MemoryStream();
        using (var w = new BinaryWriter(stream, Encoding.UTF8, leaveOpen: true))
        {
            switch (message)
            {
                case HelloMessage m:
                    w.Write(m.Protocol);
                    w.Write(m.Name);
                    w.Write(m.Mods);
                    break;
                case WelcomeMessage m:
                    w.Write(m.Player);
                    break;
                case RefusedMessage m:
                    w.Write(m.Reason);
                    break;
                case CommandMessage m:
                    Bytes(w, m.Command);
                    break;
                case StateMessage m:
                    w.Write(m.Tick);
                    w.Write(m.Hash);
                    w.Write(m.Full);
                    w.Write(m.Length);
                    Bytes(w, m.Payload);
                    w.Write((int)m.Events);
                    w.Write(m.Notices.Length);
                    foreach (var n in m.Notices)
                    {
                        w.Write((int)n.Kind);
                        w.Write(n.Recipient);
                        w.Write(n.Value);
                        w.Write(n.Other);
                        w.Write(n.Place);
                        w.Write(n.Ship);
                        w.Write(n.Count);
                        w.Write(n.Named);
                        w.Write(n.Text ?? "");
                    }

                    break;
                case SnapshotRequestMessage:
                    break;
                case ChatMessage m:
                    w.Write(m.From);
                    w.Write(m.Text);
                    break;
                case SessionNewsMessage m:
                    w.Write((byte)m.Kind);
                    w.Write(m.Player);
                    w.Write(m.Value);
                    break;
                default:
                    if (LobbyCodec.TypeOf(message) is null)
                    {
                        throw new ArgumentException(message.GetType().Name, nameof(message));
                    }

                    LobbyCodec.Write(w, message);
                    break;
            }
        }

        return (TypeOf(message), stream.ToArray());
    }

    public static NetMessage Decode(MessageType type, byte[] body)
    {
        using var r = new BinaryReader(new MemoryStream(body), Encoding.UTF8);
        return type switch
        {
            MessageType.Hello => new HelloMessage(r.ReadInt32(), r.ReadString(), r.ReadString()),
            MessageType.Welcome => new WelcomeMessage(r.ReadInt32()),
            MessageType.Refused => new RefusedMessage(r.ReadString()),
            MessageType.Command => new CommandMessage(Bytes(r)),
            MessageType.State => ReadState(r),
            MessageType.SnapshotRequest => new SnapshotRequestMessage(),
            MessageType.Chat => new ChatMessage(r.ReadInt32(), r.ReadString()),
            MessageType.SessionNews => new SessionNewsMessage((SessionNews)r.ReadByte(), r.ReadInt32(), r.ReadInt32()),
            _ => LobbyCodec.Read(type, r) ?? throw new InvalidDataException(NetText.UnknownMessage),
        };
    }

    private static StateMessage ReadState(BinaryReader r)
    {
        var tick = r.ReadInt64();
        var hash = r.ReadUInt64();
        var full = r.ReadBoolean();
        var length = r.ReadInt32();
        var payload = Bytes(r);
        var events = (StepEvents)r.ReadInt32();
        var notices = new NoticeData[r.ReadInt32()];
        for (var i = 0; i < notices.Length; i++)
        {
            notices[i] = new NoticeData((NoticeKind)r.ReadInt32(), r.ReadInt32(), r.ReadInt32(), r.ReadInt32(), r.ReadInt32(), r.ReadInt32(), r.ReadInt32(), r.ReadBoolean()) { Text = Nonempty(r.ReadString()) };
        }

        return new StateMessage(tick, hash, full, length, payload, events, notices);
    }

    private static string? Nonempty(string text) => text.Length == 0 ? null : text;

    private static MessageType TypeOf(NetMessage message) => message switch
    {
        HelloMessage => MessageType.Hello,
        WelcomeMessage => MessageType.Welcome,
        RefusedMessage => MessageType.Refused,
        CommandMessage => MessageType.Command,
        StateMessage => MessageType.State,
        SnapshotRequestMessage => MessageType.SnapshotRequest,
        ChatMessage => MessageType.Chat,
        SessionNewsMessage => MessageType.SessionNews,
        _ => LobbyCodec.TypeOf(message) ?? throw new ArgumentException(message.GetType().Name, nameof(message)),
    };

    private static void Bytes(BinaryWriter w, byte[] bytes)
    {
        w.Write(bytes.Length);
        w.Write(bytes);
    }

    private static byte[] Bytes(BinaryReader r)
    {
        var length = r.ReadInt32();
        if (length < 0 || length > NetProtocol.MaxMessage)
        {
            throw new InvalidDataException(NetText.MessageTooLarge);
        }

        return r.ReadBytes(length);
    }
}
