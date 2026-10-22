using System.Collections.Concurrent;
using System.Net;
using System.Net.Sockets;
using OpenPax.Net.Messages;
using OpenPax.Net.State;
using OpenPax.Sim;
using OpenPax.Sim.Commands;
using OpenPax.Sim.Saving;
using OpenPax.Sim.Time;

namespace OpenPax.Net.Server;

/// <summary>
/// The one simulation of a network game (docs/netcode/modern.md). Clients
/// send commands; the server applies them between ticks, steps the game on
/// its clock and sends every client the state, as a difference from the
/// last it sent that client, with the hash and that player's events and
/// notices. It runs inside the hosting player's game, whose own player is
/// played on the same game directly, or headless.
/// </summary>
public sealed class GameServer : IDisposable
{
    /// <summary>States go out at most this often; a burst of battle ticks is one update.</summary>
    public const int UpdateInterval = 100;

    /// <summary>The longest chat line passed on.</summary>
    public const int MaxChat = 200;

    private readonly List<RemotePlayer> _clients = [];
    private readonly ConcurrentQueue<Connection> _arriving = new();
    private readonly List<(long Tick, Command Command)> _log = [];
    private readonly List<ChatMessage> _chat = [];
    private readonly List<SessionNewsMessage> _news = [];
    private int? _speedSent;

    /// <summary>How long a pause vote waits for its answers; silence counts as no.</summary>
    public const int PauseVoteTime = 30_000;

    // An open pause vote: the seats still to answer, and when it lapses.
    private (HashSet<int> Waiting, long Deadline)? _vote;
    private long _now;

    /// <summary>A pause vote is open.</summary>
    public bool Voting => _vote is not null;

    /// <summary>
    /// The host asks to pause (speed 0, with the clock's ZeroPauses): with
    /// nobody else seated it pauses at once; else every seated player is
    /// asked, and the game pauses only when all say yes (docs/netcode/modern.md).
    /// The game goes on at its speed while they answer.
    /// </summary>
    public void RequestPause()
    {
        var others = _clients.Where(c => c.Player >= 0 && !c.Connection.Closed).Select(c => c.Player).ToHashSet();
        if (others.Count == 0)
        {
            Clock.SetSpeed(0);
            return;
        }

        if (_vote is null)
        {
            _vote = (others, _now + PauseVoteTime);
            Tell(new SessionNewsMessage(SessionNews.PauseVoteOpened, _host, 1), includeHost: false);
        }
    }

    private void CloseVote(bool passed)
    {
        _vote = null;
        if (passed)
        {
            Clock.SetSpeed(0);
        }

        Tell(new SessionNewsMessage(SessionNews.PauseVoteClosed, _host, passed ? 1 : 0), includeHost: true);
    }
    private readonly string _mods;
    private readonly int _host;
    private TcpListener? _listener;
    private long? _lastUpdate;
    private bool _changed = true;

    /// <param name="host">The hosting player's index, played on this game directly; -1 for a dedicated server.</param>
    public GameServer(Game game, GameClock clock, int host, string mods = "")
    {
        Game = game;
        Clock = clock;
        _host = host;
        _mods = mods;
    }

    public Game Game { get; }

    public GameClock Clock { get; }

    /// <summary>Every command applied, with the tick it was applied before, for replays.</summary>
    public IReadOnlyList<(long Tick, Command Command)> Log => _log;

    /// <summary>The players seated by clients.</summary>
    public IEnumerable<int> Seated => _clients.Where(c => c.Player >= 0 && !c.Connection.Closed).Select(c => c.Player);

    /// <summary>Takes connections on a TCP port; 0 picks a free one. Returns the port.</summary>
    public int Listen(int port)
    {
        _listener = new TcpListener(IPAddress.Any, port);
        _listener.Start();
        var accepting = new Thread(AcceptLoop) { IsBackground = true, Name = "OpenPax server accept" };
        accepting.Start(_listener);
        return ((IPEndPoint)_listener.LocalEndpoint).Port;
    }

    /// <summary>Takes a client that arrived some other way (the lobby's relay, tests).</summary>
    public void Accept(Connection connection) => _arriving.Enqueue(connection);

    /// <summary>A line of chat from the hosting player (or the server, -1 when dedicated) to everyone.</summary>
    public void Say(string text) => Relay(new ChatMessage(_host, Trim(text)));

    /// <summary>The chat the hosting player has not seen yet, its own lines included.</summary>
    public IReadOnlyList<ChatMessage> TakeChat()
    {
        var lines = _chat.ToList();
        _chat.Clear();
        return lines;
    }

    /// <summary>The hosting player's command, applied at once as single player applies it.</summary>
    public StepEvents ApplyLocal(Command command)
    {
        return Apply(command);
    }

    /// <summary>
    /// One frame: new clients and their messages, commands applied, a tick
    /// if the clock has one due, and states sent when the interval allows.
    /// Returns what the tick asks of the host, null when none ran.
    /// </summary>
    public StepEvents? Tick(long now)
    {
        while (_arriving.TryDequeue(out var connection))
        {
            _clients.Add(new RemotePlayer(connection));
        }

        foreach (var client in _clients.ToList())
        {
            Pump(client);
        }

        // A seated player gone: everyone still here is told, and the empire
        // stays in the game with nobody giving it orders, as in the original.
        foreach (var gone in _clients.Where(c => c.Connection.Closed && c.Player >= 0).ToList())
        {
            var why = gone.Connection.Error is null ? LeaveReason.Left : LeaveReason.TimedOut;
            Tell(new SessionNewsMessage(SessionNews.PlayerLeft, gone.Player, (int)why), includeHost: true);
        }

        _clients.RemoveAll(c => c.Connection.Closed);

        // A pause vote: those who left need not answer; all yes pauses, time running out does not.
        _now = now;
        if (_vote is { } vote)
        {
            vote.Waiting.RemoveWhere(p => !_clients.Any(c => c.Player == p));
            if (vote.Waiting.Count == 0)
            {
                CloseVote(passed: true);
            }
            else if (now >= vote.Deadline)
            {
                CloseVote(passed: false);
            }
        }

        // The host's speed reaches every client, as the original's speed order does.
        if (_speedSent != Clock.Speed)
        {
            if (_speedSent is not null)
            {
                Tell(new SessionNewsMessage(SessionNews.SpeedChanged, _host, Clock.Speed), includeHost: false);
            }

            _speedSent = Clock.Speed;
        }

        StepEvents? stepped = null;
        Clock.InCombat = Game.InCombat;
        if (!Game.Over && Clock.Poll(now))
        {
            stepped = Game.Step();
            _changed = true;
        }

        if (_changed && (_lastUpdate is not { } last || now - last >= UpdateInterval))
        {
            SendStates();
            _lastUpdate = now;
            _changed = false;
        }

        return stepped;
    }

    public void Dispose()
    {
        _listener?.Stop();
        foreach (var client in _clients)
        {
            client.Connection.Dispose();
        }
    }

    private void Pump(RemotePlayer client)
    {
        while (client.Connection.TryReceive(out var message))
        {
            switch (message)
            {
                case HelloMessage hello when client.Player < 0:
                    Seat(client, hello);
                    break;
                case CommandMessage command when client.Player >= 0:
                    // A client may only command its own player, and never cheat.
                    if (ReadCommand(command.Command) is { } c && c.Player == client.Player && c is not CheatCommand)
                    {
                        Apply(c);
                    }

                    break;
                case SnapshotRequestMessage:
                    client.LastSent = null;
                    _changed = true;
                    break;
                case SessionNewsMessage { Kind: SessionNews.PauseVoteCast } answer when client.Player >= 0 && _vote is { } open && open.Waiting.Contains(client.Player):
                    if (answer.Value == 1)
                    {
                        open.Waiting.Remove(client.Player);
                    }
                    else
                    {
                        CloseVote(passed: false);
                    }

                    break;
                case ChatMessage chat when client.Player >= 0:
                    Relay(new ChatMessage(client.Player, Trim(chat.Text)));
                    break;
            }
        }
    }

    private void Seat(RemotePlayer client, HelloMessage hello)
    {
        // The seat made for this name in the room, else the first free one.
        var taken = Seated.Append(_host).ToHashSet();
        var seats = Game.Players.Where(p => !p.IsAi && !p.Eliminated && !taken.Contains(p.Index)).ToList();
        var free = seats.FirstOrDefault(p => p.Name == hello.Name) ?? seats.FirstOrDefault();
        var refusal = hello.Protocol != NetProtocol.Version ? NetText.WrongVersion
            : hello.Mods != _mods ? NetText.WrongMods
            : free is null ? NetText.GameFull
            : null;
        if (refusal is not null || free is null)
        {
            client.Connection.Send(new RefusedMessage(refusal ?? NetText.GameFull));
            client.Connection.Dispose();
            return;
        }

        client.Player = free.Index;
        client.LastSent = null;
        client.Connection.Send(new WelcomeMessage(free.Index));
        _changed = true;
    }

    private StepEvents Apply(Command command)
    {
        _log.Add((Game.Tick, command));
        _changed = true;
        return Game.Apply(command);
    }

    private Command? ReadCommand(byte[] bytes)
    {
        try
        {
            using var stream = new MemoryStream(bytes);
            return SaveFile.Read(stream, Game.Tables, null) as Command;
        }
        catch (Exception e) when (e is InvalidDataException or EndOfStreamException or NotSupportedException)
        {
            return null;
        }
    }

    private void SendStates()
    {
        var seated = _clients.Where(c => c.Player >= 0 && !c.Connection.Closed).ToList();
        if (seated.Count == 0)
        {
            return;
        }

        // Each client's events and notices come out of the game first, so the state sent holds none.
        var mail = seated.ToDictionary(c => c, c =>
        {
            var player = Game.Players[c.Player];
            return (Events: Game.TakeEvents(player), Notices: Game.TakeNotices(player).Select(n => NoticeData.Of(n, Game)).ToArray());
        });

        byte[] state;
        using (var stream = new MemoryStream())
        {
            SaveFile.Write(stream, Game, Game.Tables);
            state = stream.ToArray();
        }

        var hash = Game.Hash();
        foreach (var client in seated)
        {
            var full = client.LastSent is null;
            var payload = StateDiff.Encode(state, client.LastSent ?? []);
            var (events, notices) = mail[client];
            client.Connection.Send(new StateMessage(Game.Tick, hash, full, state.Length, payload, events, notices));
            client.LastSent = state;
        }
    }

    /// <summary>Session news since the last call, for the hosting player.</summary>
    public IReadOnlyList<SessionNewsMessage> TakeNews()
    {
        var news = _news.ToList();
        _news.Clear();
        return news;
    }

    private void Tell(SessionNewsMessage news, bool includeHost)
    {
        Broadcast(news);
        if (includeHost)
        {
            _news.Add(news);
        }
    }

    /// <summary>A chat line to every client, and kept for the hosting player.</summary>
    private void Relay(ChatMessage line)
    {
        if (line.Text.Length == 0)
        {
            return;
        }

        Broadcast(line);
        _chat.Add(line);
    }

    private static string Trim(string text)
    {
        text = text.Trim();
        return text.Length > MaxChat ? text[..MaxChat] : text;
    }

    private void Broadcast(NetMessage message)
    {
        foreach (var client in _clients.Where(c => c.Player >= 0))
        {
            client.Connection.Send(message);
        }
    }

    private void AcceptLoop(object? state)
    {
        var listener = (TcpListener)state!; // Always the listener passed by Listen.
        try
        {
            while (true)
            {
                _arriving.Enqueue(Connection.Accept(listener.AcceptTcpClient()));
            }
        }
        catch (Exception e) when (e is SocketException or ObjectDisposedException or InvalidOperationException)
        {
            // Stopped.
        }
    }
}
