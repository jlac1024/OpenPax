using System.Collections.Concurrent;
using System.Net;
using System.Net.Sockets;
using OpenPax.Net.Lan;
using OpenPax.Net.Messages;
using OpenPax.Net.Transport;
using OpenPax.Net.Wire;

namespace OpenPax.Net.Lobby;

/// <summary>
/// The lobby (docs/netcode/modern.md): players arrive with a name, see and
/// open rooms, chat, mark themselves ready, and the room's host starts the
/// game, whose traffic then goes through the lobby's relay. No accounts
/// yet: a name is enough while nobody else in the lobby has it.
/// </summary>
public sealed class LobbyServer : IDisposable
{
    public const int MaxName = 24;
    public const int MaxChat = 200;
    public const int MinPlayers = 2;
    public const int MaxPlayers = 8;

    private const int FirstMessageWait = 10_000;

    private readonly ConcurrentQueue<LobbyMember> _arriving = new();
    private readonly List<LobbyMember> _members = [];
    private readonly List<Room> _rooms = [];
    private readonly Relay _relay = new();

    // Mod packages passed from hosts to their rooms' members (docs/modding/multiplayer.md).
    private readonly ModCache _mods = new(MaxModCache);

    /// <summary>The most mod package bytes the lobby keeps; the longest unused go first.</summary>
    public const long MaxModCache = 512L * 1024 * 1024;

    /// <summary>The largest mod package passed on.</summary>
    public const long MaxModPackage = 64L * 1024 * 1024;

    private const int MaxModSet = 8192;
    private TcpListener? _listener;
    private LanBeacon? _beacon;
    private volatile int _memberCount;
    private int _nextMember = 1;
    private int _nextRoom = 1;
    private long _now;

    /// <summary>Players in the lobby now.</summary>
    public int MemberCount => _members.Count(m => m.Id > 0);

    public int RoomCount => _rooms.Count;

    /// <summary>The name LAN discovery answers with.</summary>
    public string Name { get; set; } = Environment.MachineName;

    /// <summary>Answering LAN discovery on the UDP port of the same number.</summary>
    public bool Discoverable => _beacon?.Listening == true;

    /// <summary>Takes connections on a TCP port; 0 picks a free one. Returns the port. LAN discovery is answered on that number over UDP.</summary>
    public int Listen(int port)
    {
        _listener = new TcpListener(IPAddress.Any, port);
        _listener.Start();
        new Thread(AcceptLoop) { IsBackground = true, Name = "OpenPax lobby accept" }.Start(_listener);
        var bound = ((IPEndPoint)_listener.LocalEndpoint).Port;
        _beacon = new LanBeacon(bound, () => new LanLobby(Name, "", bound, NetProtocol.Version, _memberCount));
        return bound;
    }

    /// <summary>One pass: arrivals, every member's messages, departures and lapsed relay tickets.</summary>
    public void Tick(long now)
    {
        _now = now;
        while (_arriving.TryDequeue(out var member))
        {
            _members.Add(member);
        }

        foreach (var member in _members.ToList())
        {
            while (member.Connection.TryReceive(out var message))
            {
                Handle(member, message);
            }
        }

        foreach (var gone in _members.Where(m => m.Connection.Closed).ToList())
        {
            Leave(gone);
            _mods.Forget(gone);
            _members.Remove(gone);
        }

        _relay.Expire(now);
        _memberCount = MemberCount;
    }

    public void Dispose()
    {
        _listener?.Stop();
        _beacon?.Dispose();
        foreach (var m in _members)
        {
            m.Connection.Dispose();
        }
    }

    private void Handle(LobbyMember member, NetMessage message)
    {
        if (member.Id == 0)
        {
            if (message is LobbyHelloMessage hello)
            {
                Welcome(member, hello);
            }

            return;
        }

        switch (message)
        {
            case RoomListRequestMessage:
                member.Connection.Send(RoomList());
                break;
            case CreateRoomMessage create when member.Room is null:
                Create(member, create);
                break;
            case JoinRoomMessage join when member.Room is null:
                Join(member, join);
                break;
            case LeaveRoomMessage when member.Room is not null:
                Leave(member);
                member.Connection.Send(RoomList());
                break;
            case SetReadyMessage ready when member.Room is { InGame: false } room:
                member.Ready = ready.Ready;
                SendRoom(room);
                break;
            case SeatChoiceMessage choice when member.Room is { InGame: false } room:
                member.Choice = choice.Choice;
                SendRoom(room);
                break;
            case LobbyChatMessage chat:
                Chat(member, chat.Text);
                break;
            case ModSetMessage set when set.Mods.Length <= MaxModSet:
                member.Mods = set.Mods;
                if (member.Room is { InGame: false } inRoom)
                {
                    SendRoom(inRoom);
                    if (inRoom.Host == member)
                    {
                        RoomsChanged();
                    }
                }

                break;
            case ModRequestMessage request when member.Room is { } modRoom && InSet(modRoom.Host.Mods, request.Key):
                RequestMod(member, modRoom, request.Key);
                break;
            case ModChunkMessage chunk when member.Room is { } hostRoom && hostRoom.Host == member && InSet(member.Mods, chunk.Key):
                if (_mods.Add(chunk.Key, member.Id, chunk.Total, chunk.Offset, chunk.Data, MaxModPackage) is { } done)
                {
                    foreach (var waiting in done.Waiting.Where(_members.Contains))
                    {
                        SendPackage(waiting, chunk.Key, done.Package);
                    }
                }

                break;
            case RoomSettingsMessage settings when member.Room is { InGame: false } room && room.Host == member:
                // Within what Game Setup allows; the humans count against the 16 empires.
                var s = settings.Settings;
                room.Settings = new RoomSettings(Math.Clamp(s.Stars, 20, 100), Math.Clamp(s.Opponents, 0, 16 - room.MaxPlayers), Math.Clamp(s.Difficulty, 1, 5));
                SendRoom(room);
                RoomsChanged();
                break;
            case StartGameMessage when member.Room is { InGame: false } room && room.Host == member:
                Start(room);
                break;
        }
    }

    private void Welcome(LobbyMember member, LobbyHelloMessage hello)
    {
        var name = hello.Name.Trim();
        var refusal = hello.Protocol != NetProtocol.Version ? NetText.WrongLobbyVersion
            : name.Length is 0 or > MaxName ? NetText.BadName
            : _members.Any(m => m.Id > 0 && string.Equals(m.Name, name, StringComparison.OrdinalIgnoreCase)) ? NetText.NameTaken
            : null;
        if (refusal is not null)
        {
            member.Connection.Send(new RefusedMessage(refusal));
            member.Connection.Dispose();
            return;
        }

        member.Id = _nextMember++;
        member.Name = name;
        member.Mods = hello.Mods;
        member.Connection.Send(new LobbyWelcomeMessage(member.Id));
        member.Connection.Send(RoomList());
    }

    private void Create(LobbyMember member, CreateRoomMessage create)
    {
        var name = create.Name.Trim();
        var room = new Room(_nextRoom++, name.Length == 0 ? member.Name : name[..Math.Min(name.Length, MaxName)], create.Password, Math.Clamp(create.MaxPlayers, MinPlayers, MaxPlayers), member);
        _rooms.Add(room);
        member.Room = room;
        member.Ready = false;
        SendRoom(room);
        RoomsChanged();
    }

    private void Join(LobbyMember member, JoinRoomMessage join)
    {
        var room = _rooms.FirstOrDefault(r => r.Id == join.Room);
        var refusal = room is null ? NetText.NoSuchRoom
            : room.InGame ? NetText.RoomInGame
            : room.Members.Count >= room.MaxPlayers ? NetText.RoomFull
            : room.Password.Length > 0 && room.Password != join.Password ? NetText.WrongPassword
            : null;
        if (refusal is not null || room is null)
        {
            member.Connection.Send(new RefusedMessage(refusal ?? NetText.NoSuchRoom));
            return;
        }

        room.Members.Add(member);
        member.Room = room;
        member.Ready = false;
        SendRoom(room);
        RoomsChanged();
    }

    private void Leave(LobbyMember member)
    {
        if (member.Room is not { } room)
        {
            return;
        }

        member.Room = null;
        member.Ready = false;
        room.Members.Remove(member);
        if (room.Host == member)
        {
            _rooms.Remove(room);
            foreach (var other in room.Members)
            {
                other.Room = null;
                other.Ready = false;
                other.Connection.Send(new RoomClosedMessage(NetText.HostLeft));
            }
        }
        else
        {
            SendRoom(room);
        }

        RoomsChanged();
    }

    private void Chat(LobbyMember member, string text)
    {
        text = text.Trim();
        if (text.Length == 0)
        {
            return;
        }

        var line = new LobbyChatMessage(member.Name, text[..Math.Min(text.Length, MaxChat)]);
        var hearers = member.Room is { } room ? room.Members : _members.Where(m => m.Id > 0 && m.Room is null);
        foreach (var m in hearers)
        {
            m.Connection.Send(line);
        }
    }

    /// <summary>The host starts once every other member is ready: a relay ticket for each member, the host given all of them.</summary>
    private void Start(Room room)
    {
        if (room.Members.Count < MinPlayers || room.Members.Any(m => m != room.Host && !m.Ready))
        {
            room.Host.Connection.Send(new RefusedMessage(NetText.NotAllReady));
            return;
        }

        // A member whose mods are not yet the host's is still fetching them.
        if (!room.ModsMatch)
        {
            room.Host.Connection.Send(new RefusedMessage(NetText.ModsNotSynced));
            return;
        }

        room.InGame = true;
        var tickets = room.Members.Where(m => m != room.Host).Select(m => new RelayTicket(m.Id, Relay.NewToken())).ToArray();
        foreach (var t in tickets)
        {
            _relay.Issue(t.Token, _now);
        }

        room.Host.Connection.Send(new GameStartingMessage(room.Host.Id, tickets));
        foreach (var t in tickets)
        {
            room.Members.First(m => m.Id == t.Member).Connection.Send(new GameStartingMessage(room.Host.Id, [t]));
        }

        SendRoom(room);
        RoomsChanged();
    }

    private static bool InSet(string set, string key) => set.Split(',').Contains(key, StringComparer.Ordinal);

    /// <summary>A member asks for one of the host's mods: from the lobby's copy, else the host is asked for it once and the member waits.</summary>
    private void RequestMod(LobbyMember member, Room room, string key)
    {
        if (_mods.Get(key) is { } package)
        {
            SendPackage(member, key, package);
            return;
        }

        _mods.Wait(key, member);
        if (!_mods.Coming(key) && room.Host != member)
        {
            _mods.Expect(key, room.Host.Id);
            room.Host.Connection.Send(new ModRequestMessage(key));
        }
    }

    private static void SendPackage(LobbyMember member, string key, byte[] package)
    {
        for (var offset = 0; offset < package.Length || offset == 0; offset += ModChunkMessage.MaxData)
        {
            var length = Math.Min(ModChunkMessage.MaxData, package.Length - offset);
            member.Connection.Send(new ModChunkMessage(key, package.Length, offset, package.AsSpan(offset, length).ToArray()));
            if (package.Length == 0)
            {
                break;
            }
        }
    }

    private void SendRoom(Room room)
    {
        var state = new RoomStateMessage(room.Info, room.Roster);
        foreach (var m in room.Members)
        {
            m.Connection.Send(state);
        }
    }

    /// <summary>Everyone outside a room sees the list as it changes.</summary>
    private void RoomsChanged()
    {
        var list = RoomList();
        foreach (var m in _members.Where(m => m.Id > 0 && m.Room is null))
        {
            m.Connection.Send(list);
        }
    }

    private RoomListMessage RoomList() => new([.. _rooms.Select(r => r.Info)]);

    /// <summary>
    /// Each arrival's first message says what it is: a player's hello, or a
    /// relay end with its ticket. Read here, on the accepting thread, so a
    /// relay's bytes are never taken by a message reader.
    /// </summary>
    private void AcceptLoop(object? state)
    {
        var listener = (TcpListener)state!; // Always the listener passed by Listen.
        try
        {
            while (true)
            {
                var client = listener.AcceptTcpClient();
                client.NoDelay = true;
                new Thread(() => Sort(client)) { IsBackground = true, Name = "OpenPax lobby arrival" }.Start();
            }
        }
        catch (Exception e) when (e is SocketException or ObjectDisposedException or InvalidOperationException)
        {
            // Stopped.
        }
    }

    private void Sort(TcpClient client)
    {
        try
        {
            var stream = Unwrap(client.GetStream());
            if (stream is null)
            {
                client.Dispose();
                return;
            }

            stream.ReadTimeout = FirstMessageWait;
            var first = Frames.Read(stream) is { } frame ? MessageCodec.Decode(frame.Type, frame.Body) : null;
            stream.ReadTimeout = Timeout.Infinite;
            switch (first)
            {
                case LobbyHelloMessage:
                    _arriving.Enqueue(new LobbyMember(new Connection(stream, client, first)));
                    return;
                case RelayHelloMessage relay when _relay.Arrive(relay.Token, relay.Host, stream):
                    return;
            }
        }
        catch (Exception e) when (e is IOException or InvalidDataException or EndOfStreamException or ObjectDisposedException)
        {
            // Not a player or a relay end; dropped.
        }

        client.Dispose();
    }

    /// <summary>
    /// The stream frames are read from: the socket's own for a plain TCP
    /// arrival, or a WebSocket made over it when the arrival opens with an
    /// HTTP request (a client coming through a web tunnel). Null when the
    /// request was answered without becoming one.
    /// </summary>
    private static Stream? Unwrap(NetworkStream raw)
    {
        raw.ReadTimeout = FirstMessageWait;
        var start = new byte[4];
        var got = 0;
        while (got < start.Length)
        {
            var n = raw.Read(start, got, start.Length - got);
            if (n == 0)
            {
                return null;
            }

            got += n;
        }

        if (start.AsSpan().SequenceEqual("GET "u8))
        {
            return WebSocketHandshake.Accept(raw, start) is { } socket ? new BinaryWebSocketStream(socket) : null;
        }

        return new PrefixedStream(start, raw);
    }
}
