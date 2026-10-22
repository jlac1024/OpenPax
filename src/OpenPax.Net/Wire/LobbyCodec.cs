using OpenPax.Net.Lobby;
using OpenPax.Net.Messages;

namespace OpenPax.Net.Wire;

/// <summary>The lobby's messages to and from bytes, in the same form as <see cref="MessageCodec"/>.</summary>
internal static class LobbyCodec
{
    public static MessageType? TypeOf(NetMessage message) => message switch
    {
        LobbyHelloMessage => MessageType.LobbyHello,
        LobbyWelcomeMessage => MessageType.LobbyWelcome,
        RoomListRequestMessage => MessageType.RoomListRequest,
        RoomListMessage => MessageType.RoomList,
        CreateRoomMessage => MessageType.CreateRoom,
        JoinRoomMessage => MessageType.JoinRoom,
        LeaveRoomMessage => MessageType.LeaveRoom,
        RoomStateMessage => MessageType.RoomState,
        RoomClosedMessage => MessageType.RoomClosed,
        SetReadyMessage => MessageType.SetReady,
        LobbyChatMessage => MessageType.LobbyChat,
        StartGameMessage => MessageType.StartGame,
        GameStartingMessage => MessageType.GameStarting,
        RelayHelloMessage => MessageType.RelayHello,
        RoomSettingsMessage => MessageType.RoomSettings,
        SeatChoiceMessage => MessageType.SeatChoice,
        ModSetMessage => MessageType.ModSet,
        ModRequestMessage => MessageType.ModRequest,
        ModChunkMessage => MessageType.ModChunk,
        _ => null,
    };

    public static void Write(BinaryWriter w, NetMessage message)
    {
        switch (message)
        {
            case LobbyHelloMessage m:
                w.Write(m.Protocol);
                w.Write(m.Name);
                w.Write(m.Mods);
                break;
            case LobbyWelcomeMessage m:
                w.Write(m.Member);
                break;
            case ModSetMessage m:
                w.Write(m.Mods);
                break;
            case ModRequestMessage m:
                w.Write(m.Key);
                break;
            case ModChunkMessage m:
                w.Write(m.Key);
                w.Write(m.Total);
                w.Write(m.Offset);
                w.Write(m.Data.Length);
                w.Write(m.Data);
                break;
            case RoomListMessage m:
                w.Write(m.Rooms.Length);
                foreach (var r in m.Rooms)
                {
                    Write(w, r);
                }

                break;
            case CreateRoomMessage m:
                w.Write(m.Name);
                w.Write(m.Password);
                w.Write(m.MaxPlayers);
                break;
            case JoinRoomMessage m:
                w.Write(m.Room);
                w.Write(m.Password);
                break;
            case RoomStateMessage m:
                Write(w, m.Room);
                w.Write(m.Members.Length);
                foreach (var member in m.Members)
                {
                    w.Write(member.Member);
                    w.Write(member.Name);
                    w.Write(member.Ready);
                    Write(w, member.Choice);
                    w.Write(member.Mods);
                }

                break;
            case RoomClosedMessage m:
                w.Write(m.Reason);
                break;
            case SetReadyMessage m:
                w.Write(m.Ready);
                break;
            case LobbyChatMessage m:
                w.Write(m.From);
                w.Write(m.Text);
                break;
            case GameStartingMessage m:
                w.Write(m.Host);
                w.Write(m.Tickets.Length);
                foreach (var t in m.Tickets)
                {
                    w.Write(t.Member);
                    w.Write(t.Token);
                }

                break;
            case RelayHelloMessage m:
                w.Write(m.Token);
                w.Write(m.Host);
                break;
            case RoomSettingsMessage m:
                Write(w, m.Settings);
                break;
            case SeatChoiceMessage m:
                Write(w, m.Choice);
                break;
            case RoomListRequestMessage or LeaveRoomMessage or StartGameMessage:
                break;
        }
    }

    public static NetMessage? Read(MessageType type, BinaryReader r) => type switch
    {
        MessageType.LobbyHello => new LobbyHelloMessage(r.ReadInt32(), r.ReadString(), r.ReadString()),
        MessageType.LobbyWelcome => new LobbyWelcomeMessage(r.ReadInt32()),
        MessageType.RoomListRequest => new RoomListRequestMessage(),
        MessageType.RoomList => new RoomListMessage(Many(r, ReadRoom)),
        MessageType.CreateRoom => new CreateRoomMessage(r.ReadString(), r.ReadString(), r.ReadInt32()),
        MessageType.JoinRoom => new JoinRoomMessage(r.ReadInt32(), r.ReadString()),
        MessageType.LeaveRoom => new LeaveRoomMessage(),
        MessageType.RoomState => new RoomStateMessage(ReadRoom(r), Many(r, x => new RoomMember(x.ReadInt32(), x.ReadString(), x.ReadBoolean(), ReadChoice(x)) { Mods = x.ReadString() })),
        MessageType.RoomClosed => new RoomClosedMessage(r.ReadString()),
        MessageType.SetReady => new SetReadyMessage(r.ReadBoolean()),
        MessageType.LobbyChat => new LobbyChatMessage(r.ReadString(), r.ReadString()),
        MessageType.StartGame => new StartGameMessage(),
        MessageType.GameStarting => new GameStartingMessage(r.ReadInt32(), Many(r, x => new RelayTicket(x.ReadInt32(), x.ReadString()))),
        MessageType.RelayHello => new RelayHelloMessage(r.ReadString(), r.ReadBoolean()),
        MessageType.RoomSettings => new RoomSettingsMessage(ReadSettings(r)),
        MessageType.SeatChoice => new SeatChoiceMessage(ReadChoice(r)),
        MessageType.ModSet => new ModSetMessage(r.ReadString()),
        MessageType.ModRequest => new ModRequestMessage(r.ReadString()),
        MessageType.ModChunk => ReadChunk(r),
        _ => null,
    };

    private static void Write(BinaryWriter w, RoomInfo r)
    {
        w.Write(r.Id);
        w.Write(r.Name);
        w.Write(r.Host);
        w.Write(r.Players);
        w.Write(r.MaxPlayers);
        w.Write(r.Locked);
        w.Write(r.InGame);
        w.Write(r.Mods);
        Write(w, r.Settings);
    }

    private static void Write(BinaryWriter w, RoomSettings s)
    {
        w.Write(s.Stars);
        w.Write(s.Opponents);
        w.Write(s.Difficulty);
    }

    private static RoomSettings ReadSettings(BinaryReader r) => new(r.ReadInt32(), r.ReadInt32(), r.ReadInt32());

    private static void Write(BinaryWriter w, SeatChoice c)
    {
        w.Write(c.Species);
        w.Write(c.Flag);
        w.Write(c.Color);
        var custom = c.Custom ?? [];
        w.Write(custom.Length);
        w.Write(custom);
    }

    private static SeatChoice ReadChoice(BinaryReader r)
    {
        var (species, flag, color) = (r.ReadInt32(), r.ReadInt32(), r.ReadInt32());
        var length = r.ReadInt32();
        if (length < 0 || length > SeatChoice.MaxCustom)
        {
            throw new InvalidDataException(NetText.UnknownMessage);
        }

        return new SeatChoice(species, flag, color, length == 0 ? null : r.ReadBytes(length));
    }

    private static ModChunkMessage ReadChunk(BinaryReader r)
    {
        var (key, total, offset, length) = (r.ReadString(), r.ReadInt64(), r.ReadInt64(), r.ReadInt32());
        if (length < 0 || length > ModChunkMessage.MaxData || total < 0 || offset < 0 || offset + length > total)
        {
            throw new InvalidDataException(NetText.UnknownMessage);
        }

        return new ModChunkMessage(key, total, offset, r.ReadBytes(length));
    }

    private static RoomInfo ReadRoom(BinaryReader r) =>
        new(r.ReadInt32(), r.ReadString(), r.ReadString(), r.ReadInt32(), r.ReadInt32(), r.ReadBoolean(), r.ReadBoolean(), r.ReadString(), ReadSettings(r));

    private static T[] Many<T>(BinaryReader r, Func<BinaryReader, T> read)
    {
        var count = r.ReadInt32();
        if (count < 0 || count > 10000)
        {
            throw new InvalidDataException(NetText.MessageTooLarge);
        }

        var items = new T[count];
        for (var i = 0; i < count; i++)
        {
            items[i] = read(r);
        }

        return items;
    }
}
