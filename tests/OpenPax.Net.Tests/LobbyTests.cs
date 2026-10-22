using OpenPax.Net.Client;
using OpenPax.Net.Lobby;
using OpenPax.Net.Server;
using OpenPax.Sim.Setup;
using OpenPax.Sim.Tests;
using OpenPax.Sim.Time;

namespace OpenPax.Net.Tests;

/// <summary>The lobby over loopback: rooms, chat, the ready check, and a game carried by the relay.</summary>
public sealed class LobbyTests : IDisposable
{
    private readonly LobbyServer _lobby = new();
    private readonly List<LobbyClient> _players = [];
    private readonly int _port;
    private long _now;

    public LobbyTests()
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

    private const int ModChunkMessageMax = OpenPax.Net.Messages.ModChunkMessage.MaxData;

    private LobbyClient Arrive(string name, string mods = "")
    {
        var p = new LobbyClient("127.0.0.1", _port, name, mods);
        _players.Add(p);
        return p;
    }

    private void Frame(Action? more = null)
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

    private void Until(Func<bool> done, string what, Action? more = null)
    {
        for (var i = 0; i < 3000 && !done(); i++)
        {
            Frame(more);
        }

        Assert.True(done(), what);
    }

    [Fact]
    public void PlayersMeetInARoomAndPlayThroughTheRelay()
    {
        var (ann, bo, cy) = (Arrive("Ann"), Arrive("Bo"), Arrive("Cy"));
        Until(() => _players.All(p => p.Member > 0), "everyone is in the lobby");

        ann.CreateRoom("Ann's game", "secret", 4);
        Until(() => ann.Room is not null && bo.Rooms.Count == 1, "the room is listed");
        Assert.True(ann.IsHost);
        Assert.True(bo.Rooms[0].Locked);

        bo.JoinRoom(bo.Rooms[0].Id, "wrong");
        Until(() => bo.Refusal is not null, "a wrong password is refused");
        Assert.Equal(NetText.WrongPassword, bo.Refusal);
        bo.JoinRoom(bo.Rooms[0].Id, "secret");
        cy.JoinRoom(cy.Rooms[0].Id, "secret");
        Until(() => ann.Members.Count == 3, "both join");

        bo.Say("hello");
        var heard = new List<Messages.LobbyChatMessage>();
        Until(() => heard.Any(c => c.From == "Bo" && c.Text == "hello"), "the room hears chat", () => heard.AddRange(ann.TakeChat()));

        // Not everyone is ready yet.
        bo.SetReady(true);
        Until(() => ann.Members.Count(m => m.Ready) == 1, "Bo is ready");
        ann.StartGame();
        Until(() => ann.Refusal is not null, "the start is refused");
        Assert.Equal(NetText.NotAllReady, ann.Refusal);

        cy.SetReady(true);
        Until(() => ann.Members.Count(m => m.Ready) == 2, "Cy is ready");
        ann.StartGame();
        Until(() => _players.All(p => p.Starting is not null), "the game starts for all");
        Assert.Equal(2, ann.Starting!.Tickets.Length); // Not null: just waited for.
        Assert.True(_players.All(p => p.Room?.InGame == true));

        // The host runs the server and opens a relay end per member; each member opens its own.
        var tables = GameTablesForTests.Load();
        var humans = ann.Members.Select((m, i) => new HumanPlayerSetup(m.Name, i, i, tables.Species[i])).ToList();
        var game = NewGame.Create(new GameSetup(99, 30, 4, 3, humans), tables);
        using var server = new GameServer(game, new GameClock(0), host: 0);
        foreach (var t in ann.Starting.Tickets)
        {
            server.Accept(ann.OpenRelay(t, host: true));
        }

        var clients = new[] { bo, cy }.Select(p => new GameClient(p.OpenRelay(p.Starting!.Tickets[0], host: false), tables, p.Room!.Name)).ToList(); // Not null: started and in the room.
        long gameTime = 0;
        void Play()
        {
            gameTime += 50;
            server.Tick(gameTime);
            foreach (var c in clients)
            {
                c.Pump();
            }
        }

        Until(() => clients.All(c => c.Game is not null && c.Player > 0), "the members' games arrive through the relay", Play);
        server.Clock.Paused = true;
        Until(() => clients.All(c => c.Game!.Hash() == game.Hash()), "they match the host", Play);
        foreach (var c in clients)
        {
            c.Dispose();
        }

        // The host leaving closes the room for the others.
        ann.LeaveRoom();
        Until(() => bo.Room is null && cy.Room is null, "the room closes");
        Assert.Equal(NetText.HostLeft, bo.Refusal);
    }

    [Fact]
    public void NamesVersionsAndModsAreChecked()
    {
        // Arrivals are sorted on their own threads, so the twin comes only once Ann is in.
        var first = Arrive("Ann");
        Until(() => first.Member > 0, "Ann is in");
        var twin = Arrive("ann");
        Until(() => twin.Refusal is not null, "a taken name is refused");
        Assert.Equal(NetText.NameTaken, twin.Refusal);

        var modded = Arrive("Dee", "somemod@1#abc");
        Until(() => modded.Member > 0, "mods do not keep anyone out of the lobby");
    }

    [Fact]
    public void AJoinerFetchesTheHostsModsThroughTheLobbyAndTheStartWaitsForThem()
    {
        const string Key = "big@1#0123456789ab";
        var package = Enumerable.Range(0, (ModChunkMessageMax * 2) + 1000).Select(i => (byte)(i * 7)).ToArray(); // three pieces
        var host = Arrive("Ann", Key);
        var joiner = Arrive("Bo");
        var asked = 0;
        host.PackageFor = key =>
        {
            asked++;
            return key == Key ? package : null;
        };
        Until(() => host.Member > 0 && joiner.Member > 0, "both in");
        host.CreateRoom("modded", "", 3);
        Until(() => joiner.Rooms.Count == 1, "listed");
        Assert.Equal(Key, joiner.Rooms[0].Mods);

        joiner.JoinRoom(joiner.Rooms[0].Id, "");
        Until(() => host.Members.Count == 2, "another mod set may join, to fetch it");
        Assert.Equal("", host.Members[1].Mods);

        joiner.SetReady(true);
        Until(() => host.Members[1].Ready, "ready");
        host.StartGame();
        Until(() => host.Refusal is not null, "the start waits for the mods");
        Assert.Equal(NetText.ModsNotSynced, host.Refusal);

        joiner.RequestMod("other@1#ffffffffffff"); // not the room's: the lobby passes it nowhere
        joiner.RequestMod(Key);
        var received = new List<(string Key, byte[] Package)>();
        Until(() => received.Count == 1, "the package arrives whole", () => received.AddRange(joiner.TakeMods()));
        Assert.Equal(Key, received[0].Key);
        Assert.Equal(package, received[0].Package);
        Assert.Equal(1, asked);

        // A second joiner gets the lobby's copy without the host being asked again.
        var third = Arrive("Cy");
        Until(() => third.Member > 0, "Cy in");
        third.JoinRoom(third.Rooms[0].Id, "");
        Until(() => host.Members.Count == 3, "Cy joins");
        third.RequestMod(Key);
        var again = new List<(string Key, byte[] Package)>();
        Until(() => again.Count == 1, "from the lobby's copy", () => again.AddRange(third.TakeMods()));
        Assert.Equal(1, asked);

        joiner.SetMods(Key);
        third.SetMods(Key);
        third.SetReady(true);
        Until(() => host.Members.All(m => m.Mods == Key) && host.Members.Count(m => m.Ready) == 2, "everyone has the host's mods");
        host.Refusal = null;
        host.StartGame();
        Until(() => host.Starting is not null, "now the game starts");
    }
}
