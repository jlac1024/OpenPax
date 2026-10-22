using OpenPax.Net.Messages;
using OpenPax.Net.State;
using OpenPax.Sim;
using OpenPax.Sim.Commands;
using OpenPax.Sim.Saving;
using OpenPax.Sim.Setup;

namespace OpenPax.Net.Client;

/// <summary>
/// A player's end of a network game. It never steps the game: it sends
/// commands and takes each state the server sends, merging it into the one
/// game its screens hold and checking the hash. A state that does not
/// match, or cannot be rebuilt, is a desync: it is counted and the whole
/// game is asked for again.
/// </summary>
public sealed class GameClient : IDisposable
{
    private readonly Connection _connection;
    private readonly GameTables _tables;
    private readonly List<GameNotice> _notices = [];
    private readonly List<NoticeData> _pendingNotices = [];
    private readonly List<ChatMessage> _chat = [];
    private readonly List<SessionNewsMessage> _news = [];
    private StepEvents _events;
    private byte[]? _last;
    private bool _awaitingSnapshot;

    public GameClient(Connection connection, GameTables tables, string name, string mods = "")
    {
        _connection = connection;
        _tables = tables;
        connection.Send(new HelloMessage(NetProtocol.Version, name, mods));
    }

    /// <summary>The player this client plays, once the server has seated it; -1 before.</summary>
    public int Player { get; private set; } = -1;

    /// <summary>Why the server turned this client away, if it did.</summary>
    public string? Refusal { get; private set; }

    /// <summary>The game, from the first whole state on; the same instance for the rest of the game.</summary>
    public Game? Game { get; private set; }

    /// <summary>States that did not match the server's hash, or could not be rebuilt.</summary>
    public int Desyncs { get; private set; }

    public bool Connected => !_connection.Closed;

    /// <summary>Handles what has arrived; true when the game changed.</summary>
    public bool Pump()
    {
        var changed = false;
        while (_connection.TryReceive(out var message))
        {
            switch (message)
            {
                case WelcomeMessage welcome:
                    Player = welcome.Player;
                    break;
                case RefusedMessage refused:
                    Refusal = refused.Reason;
                    break;
                case StateMessage state:
                    changed |= Take(state);
                    break;
                case ChatMessage chat:
                    _chat.Add(chat);
                    break;
                case SessionNewsMessage news:
                    _news.Add(news);
                    break;
            }
        }

        return changed;
    }

    public void Send(Command command)
    {
        using var stream = new MemoryStream();
        SaveFile.Write(stream, command, _tables);
        _connection.Send(new CommandMessage(stream.ToArray()));
    }

    public void Say(string text) => _connection.Send(new ChatMessage(Player, text));

    /// <summary>This player's answer to an open pause vote.</summary>
    public void VotePause(bool yes) => _connection.Send(new SessionNewsMessage(SessionNews.PauseVoteCast, Player, yes ? 1 : 0));

    /// <summary>The events for this client's player since the last call.</summary>
    public StepEvents TakeEvents()
    {
        var events = _events;
        _events = StepEvents.None;
        return events;
    }

    public IReadOnlyList<GameNotice> TakeNotices()
    {
        var notices = _notices.ToList();
        _notices.Clear();
        return notices;
    }

    public IReadOnlyList<SessionNewsMessage> TakeNews()
    {
        var news = _news.ToList();
        _news.Clear();
        return news;
    }

    public IReadOnlyList<ChatMessage> TakeChat()
    {
        var chat = _chat.ToList();
        _chat.Clear();
        return chat;
    }

    /// <summary>Spoils the last state kept, as a lost or damaged update would; for tests of recovery.</summary>
    public void SpoilLastState()
    {
        // Bytes all through it, so the damage is sure to reach something the
        // state hash covers (one byte in the middle could land in a name).
        if (_last is { Length: > 0 })
        {
            for (var i = _last.Length / 8; i < _last.Length; i += 97)
            {
                _last[i] ^= 0x5A;
            }
        }
    }

    public void Dispose() => _connection.Dispose();

    private bool Take(StateMessage state)
    {
        // Events and notices count even if the state itself must be asked for
        // again; notices are found in the game once a state is taken.
        _events |= state.Events;
        _pendingNotices.AddRange(state.Notices);
        if (!state.Full && (_last is null || _awaitingSnapshot))
        {
            return false;
        }

        Game? newer = null;
        byte[]? bytes = null;
        try
        {
            bytes = StateDiff.Decode(state.Payload, state.Length, state.Full ? [] : _last);
            using var stream = new MemoryStream(bytes);
            newer = SaveFile.Read(stream, _tables, null) as Game;
        }
        catch (Exception e) when (e is InvalidDataException or EndOfStreamException or NotSupportedException or InvalidCastException or ArgumentException or IndexOutOfRangeException or KeyNotFoundException)
        {
            newer = null;
        }

        if (newer is null || bytes is null || newer.Hash() != state.Hash)
        {
            Desync();
            return false;
        }

        if (Game is null)
        {
            Game = newer;
        }
        else
        {
            StateMerge.Into(Game, newer);
        }

        if (Game.Hash() != state.Hash)
        {
            Desync();
            return false;
        }

        _last = bytes;
        _awaitingSnapshot = false;
        _notices.AddRange(_pendingNotices.Select(n => n.ToNotice(Game)));
        _pendingNotices.Clear();
        return true;
    }

    private void Desync()
    {
        Desyncs++;
        _last = null;
        _awaitingSnapshot = true;
        _connection.Send(new SnapshotRequestMessage());
    }
}
