using OpenPax.Net.Messages;
using OpenPax.Net.Wire;

namespace OpenPax.Net.Lobby;

/// <summary>
/// A player's end of the lobby: what it knows of the rooms and its own
/// room, the chat heard, and once the game starts the tickets for the
/// relay connections that carry it.
/// </summary>
public sealed class LobbyClient : IDisposable
{
    private readonly Connection _connection;
    private readonly LobbyAddress _address;
    private readonly List<LobbyChatMessage> _chat = [];

    // Mod packages (docs/modding/multiplayer.md): pieces still to send as host, packages coming in, and those whole.
    private readonly Queue<ModChunkMessage> _sending = new();
    private readonly Dictionary<string, MemoryStream> _receiving = new(StringComparer.Ordinal);
    private readonly List<(string Key, byte[] Package)> _received = [];

    // How many pieces go out each pump, so a big package does not hold up the game's frame.
    private const int ChunksPerPump = 8;

    // Set on leaving a room until the next create or join: a room's state the
    // lobby sent before it saw the leave must not put the player back in.
    private bool _left;

    public LobbyClient(string host, int port, string name, string mods = "")
        : this(new LobbyAddress(host, port), name, mods)
    {
    }

    public LobbyClient(LobbyAddress address, string name, string mods = "")
    {
        _address = address;
        _connection = new Connection(address.Open());
        _connection.Send(new LobbyHelloMessage(NetProtocol.Version, name, mods));
    }

    /// <summary>This player's member number once the lobby has taken it in, else 0.</summary>
    public int Member { get; private set; }

    public IReadOnlyList<RoomInfo> Rooms { get; private set; } = [];

    /// <summary>The room this player is in, or null.</summary>
    public RoomInfo? Room { get; private set; }

    public IReadOnlyList<RoomMember> Members { get; private set; } = [];

    public bool IsHost => Room is not null && Members.Count > 0 && Members[0].Member == Member;

    /// <summary>The last thing the lobby turned down, and why; cleared when read.</summary>
    public string? Refusal { get; set; }

    /// <summary>Set once the room's game starts: the host's member number and this end's tickets.</summary>
    public GameStartingMessage? Starting { get; private set; }

    public bool Connected => !_connection.Closed;

    /// <summary>
    /// As host, a package of one of this player's mods by its key, for the
    /// lobby to pass to the room; null for none. Set by the game.
    /// </summary>
    public Func<string, byte[]?>? PackageFor { get; set; }

    /// <summary>Packages still coming in: key, bytes so far, bytes in all.</summary>
    public IReadOnlyList<(string Key, long Have, long Total)> Downloads =>
        [.. _receiving.Select(r => (r.Key, r.Value.Length, _totals.GetValueOrDefault(r.Key)))];

    private readonly Dictionary<string, long> _totals = new(StringComparer.Ordinal);

    /// <summary>This player's mods as they are now, after another set was loaded in place.</summary>
    public void SetMods(string mods) => _connection.Send(new ModSetMessage(mods));

    /// <summary>Asks for one of the room host's mods; it arrives in pieces (TakeMods).</summary>
    public void RequestMod(string key)
    {
        if (!_receiving.ContainsKey(key))
        {
            _receiving[key] = new MemoryStream();
            _connection.Send(new ModRequestMessage(key));
        }
    }

    /// <summary>The mod packages that came in whole since the last call.</summary>
    public IReadOnlyList<(string Key, byte[] Package)> TakeMods()
    {
        var done = _received.ToList();
        _received.Clear();
        return done;
    }

    /// <summary>Handles what has arrived; true when anything changed.</summary>
    public bool Pump()
    {
        var changed = false;
        while (_connection.TryReceive(out var message))
        {
            changed = true;
            switch (message)
            {
                case LobbyWelcomeMessage welcome:
                    Member = welcome.Member;
                    break;
                case RefusedMessage refused:
                    Refusal = refused.Reason;
                    break;
                case RoomListMessage list:
                    Rooms = list.Rooms;
                    break;
                case RoomStateMessage when _left:
                    break;
                case RoomStateMessage state:
                    Room = state.Room;
                    Members = state.Members;
                    break;
                case RoomClosedMessage closed:
                    Room = null;
                    Members = [];
                    Refusal = closed.Reason;
                    break;
                case LobbyChatMessage chat:
                    _chat.Add(chat);
                    break;
                case GameStartingMessage starting:
                    Starting = starting;
                    break;
                case ModRequestMessage request when PackageFor?.Invoke(request.Key) is { } package:
                    Queue(request.Key, package);
                    break;
                case ModChunkMessage chunk when _receiving.TryGetValue(chunk.Key, out var bytes) && chunk.Offset == bytes.Length:
                    bytes.Write(chunk.Data);
                    _totals[chunk.Key] = chunk.Total;
                    if (bytes.Length >= chunk.Total)
                    {
                        _receiving.Remove(chunk.Key);
                        _totals.Remove(chunk.Key);
                        _received.Add((chunk.Key, bytes.ToArray()));
                    }

                    break;
            }
        }

        for (var i = 0; i < ChunksPerPump && _sending.TryDequeue(out var piece); i++)
        {
            _connection.Send(piece);
        }

        return changed;
    }

    private void Queue(string key, byte[] package)
    {
        for (var offset = 0; offset < package.Length || offset == 0; offset += ModChunkMessage.MaxData)
        {
            var length = Math.Min(ModChunkMessage.MaxData, package.Length - offset);
            _sending.Enqueue(new ModChunkMessage(key, package.Length, offset, package.AsSpan(offset, length).ToArray()));
            if (package.Length == 0)
            {
                break;
            }
        }
    }

    public void RefreshRooms() => _connection.Send(new RoomListRequestMessage());

    public void CreateRoom(string name, string password, int maxPlayers)
    {
        _left = false;
        _connection.Send(new CreateRoomMessage(name, password, maxPlayers));
    }

    public void JoinRoom(int room, string password)
    {
        _left = false;
        _connection.Send(new JoinRoomMessage(room, password));
    }

    public void LeaveRoom()
    {
        _left = true;
        _connection.Send(new LeaveRoomMessage());
        Room = null;
        Members = [];
    }

    public void SetReady(bool ready) => _connection.Send(new SetReadyMessage(ready));

    public void Say(string text) => _connection.Send(new LobbyChatMessage("", text));

    public void StartGame() => _connection.Send(new StartGameMessage());

    /// <summary>This member chooses what to play as.</summary>
    public void Choose(SeatChoice choice) => _connection.Send(new SeatChoiceMessage(choice));

    /// <summary>The host sets the room's galaxy.</summary>
    public void SetSettings(RoomSettings settings) => _connection.Send(new RoomSettingsMessage(settings));

    public IReadOnlyList<LobbyChatMessage> TakeChat()
    {
        var chat = _chat.ToList();
        _chat.Clear();
        return chat;
    }

    /// <summary>A connection through the lobby's relay for a ticket: the host opens one per member, a member its one.</summary>
    public Connection OpenRelay(RelayTicket ticket, bool host)
    {
        var stream = _address.Open();
        var (type, body) = MessageCodec.Encode(new RelayHelloMessage(ticket.Token, host));
        Frames.Write(stream, type, body);
        return new Connection(stream);
    }

    public void Dispose() => _connection.Dispose();
}
