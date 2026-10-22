using System.Net.Sockets;
using System.Text;
using OpenPax.Net.Client;
using OpenPax.Net.Lobby;
using OpenPax.Net.Server;
using OpenPax.Sim.Setup;
using OpenPax.Sim.Tests;
using OpenPax.Sim.Time;

namespace OpenPax.Net.Tests;

/// <summary>The lobby reached as a WebSocket, as through a Cloudflare tunnel, on the same port as plain TCP players.</summary>
public sealed class LobbyWebSocketTests : IDisposable
{
    private readonly LobbyServer _lobby = new();
    private readonly List<LobbyClient> _players = [];
    private readonly int _port;
    private long _now;

    public LobbyWebSocketTests()
    {
        _port = _lobby.Listen(0);
    }

    public void Dispose()
    {
        foreach (var p in _players)
        {
            p.Dispose();
        }

        _lobby.Dispose();
    }

    private LobbyClient Arrive(string address, string name)
    {
        Assert.True(LobbyAddress.TryParse(address, out var a));
        var p = new LobbyClient(a, name);
        _players.Add(p);
        return p;
    }

    private void Until(Func<bool> done, string what, Action? more = null)
    {
        for (var i = 0; i < 3000 && !done(); i++)
        {
            _now += 20;
            _lobby.Tick(_now);
            Thread.Sleep(2);
            foreach (var p in _players)
            {
                p.Pump();
            }

            more?.Invoke();
        }

        Assert.True(done(), what);
    }

    [Fact]
    public void AWebSocketPlayerHostsAPlainOneAndTheGameCrossesTheRelay()
    {
        var ann = Arrive("ws://127.0.0.1:" + _port + "/", "Ann");
        var bo = Arrive("127.0.0.1:" + _port, "Bo");
        Until(() => _players.All(p => p.Member > 0), "both are in the lobby");

        ann.CreateRoom("Ann's game", "", 4);
        Until(() => bo.Rooms.Count == 1, "Bo sees the room");
        bo.JoinRoom(bo.Rooms[0].Id, "");
        Until(() => ann.Members.Count == 2, "Bo joins");
        bo.SetReady(true);
        Until(() => ann.Members.Count(m => m.Ready) == 1, "Bo is ready");
        ann.StartGame();
        Until(() => _players.All(p => p.Starting is not null), "the game starts");

        // Ann's relay end goes over a WebSocket, Bo's over TCP.
        var tables = GameTablesForTests.Load();
        var humans = ann.Members.Select((m, i) => new HumanPlayerSetup(m.Name, i, i, tables.Species[i])).ToList();
        var game = NewGame.Create(new GameSetup(7, 30, 3, 3, humans), tables);
        using var server = new GameServer(game, new GameClock(0), host: 0);
        server.Accept(ann.OpenRelay(ann.Starting!.Tickets[0], host: true)); // Not null: just waited for.
        using var client = new GameClient(bo.OpenRelay(bo.Starting!.Tickets[0], host: false), tables, "Bo"); // As above.
        long gameTime = 0;
        void Play()
        {
            gameTime += 50;
            server.Tick(gameTime);
            client.Pump();
        }

        Until(() => client.Game is not null && client.Player > 0, "Bo's game arrives", Play);
        server.Clock.Paused = true;
        Until(() => client.Game!.Hash() == game.Hash(), "it matches the host's", Play);
    }

    [Fact]
    public void APlainWebRequestIsAnsweredAndDropped()
    {
        using var tcp = new TcpClient("127.0.0.1", _port);
        var stream = tcp.GetStream();
        stream.Write("GET / HTTP/1.1\r\nHost: lobby\r\n\r\n"u8);
        var answer = new StreamReader(stream, Encoding.ASCII).ReadToEnd();
        Assert.StartsWith("HTTP/1.1 200", answer, StringComparison.Ordinal);
        Assert.Contains("OpenPax lobby", answer, StringComparison.Ordinal);
    }
}
