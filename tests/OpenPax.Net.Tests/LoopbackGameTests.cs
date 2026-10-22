using OpenPax.Net.Client;
using OpenPax.Net.Server;
using OpenPax.Sim;
using OpenPax.Sim.Commands;
using OpenPax.Sim.Players;
using OpenPax.Sim.Setup;
using OpenPax.Sim.Tests;
using OpenPax.Sim.Time;

namespace OpenPax.Net.Tests;

/// <summary>A dedicated server and three clients over loopback TCP, as the Phase 5 exit asks of a LAN game.</summary>
public sealed class LoopbackGameTests : IDisposable
{
    private readonly GameTables _tables = GameTablesForTests.Load();
    private readonly GameServer _server;
    private readonly List<GameClient> _clients = [];
    private long _now;

    public LoopbackGameTests()
    {
        var humans = new[] { "Ann", "Bo", "Cy" }.Select((n, i) => new HumanPlayerSetup(n, i, i, _tables.Species[i])).ToList();
        var game = NewGame.Create(new GameSetup(777, 30, 5, 3, humans), _tables);
        var clock = new GameClock(0);
        clock.SetSpeed(6);
        _server = new GameServer(game, clock, host: -1);
    }

    public void Dispose()
    {
        foreach (var c in _clients)
        {
            c.Dispose();
        }

        _server.Dispose();
    }

    private void Frame()
    {
        _now += 50;
        _server.Tick(_now);
        Thread.Sleep(2);
        foreach (var c in _clients)
        {
            c.Pump();
        }
    }

    private void Until(Func<bool> done, string what)
    {
        for (var i = 0; i < 3000 && !done(); i++)
        {
            Frame();
        }

        Assert.True(done(), what + ": " + string.Join(", ", _clients.Select(c => $"player {c.Player} desyncs {c.Desyncs} connected {c.Connected} game {c.Game?.Tick}")) + $" server {_server.Game.Tick}");
    }

    private bool AllCaughtUp() => _clients.All(c => c.Game is { } g && g.Tick == _server.Game.Tick && g.Hash() == _server.Game.Hash());

    [Fact]
    public void ThreeClientsPlayOneGameAndADesyncIsCaughtAndRecovered()
    {
        var port = _server.Listen(0);
        foreach (var name in new[] { "Ann", "Bo", "Cy" })
        {
            _clients.Add(new GameClient(Connection.Connect("127.0.0.1", port), _tables, name));
        }

        Until(() => _clients.All(c => c.Player >= 0 && c.Game is not null), "every client is seated and has the game");
        Assert.Equal([0, 1, 2], _clients.Select(c => c.Player).Order());

        // Each client orders its own first fleet to intercept; the server applies it.
        foreach (var c in _clients)
        {
            var fleet = c.Game!.Players[c.Player].Fleets.First(f => f.Ships.Count > 0);
            c.Send(new FleetOrderCommand(c.Player, fleet.ObjectId, FleetOrderKind.ToggleIntercept, -1, 0, 0, false));
        }

        for (var i = 0; i < 200; i++)
        {
            Frame();
        }

        _server.Clock.Paused = true;
        Until(AllCaughtUp, "every client matches the server");
        Assert.All(_clients, c => Assert.Contains(c.Game!.Players[c.Player].Fleets, f => f.Intercepting));
        Assert.All(_clients, c => Assert.Equal(0, c.Desyncs));
        Assert.Equal(3, _server.Log.Count);

        // A damaged update: the client notices, asks for the whole game and is whole again.
        _clients[1].SpoilLastState();
        _server.Clock.Paused = false;
        for (var i = 0; i < 40; i++)
        {
            Frame();
        }

        _server.Clock.Paused = true;
        Until(AllCaughtUp, "the spoiled client recovers");
        Assert.Equal(1, _clients[1].Desyncs);
        Assert.Equal(0, _clients[0].Desyncs);
    }

    [Fact]
    public void AClientIsNotLetCommandAnotherPlayerOrCheat()
    {
        var port = _server.Listen(0);
        var client = new GameClient(Connection.Connect("127.0.0.1", port), _tables, "Ann");
        _clients.Add(client);
        Until(() => client.Game is not null, "seated");
        var hash = _server.Game.Hash();
        _server.Clock.Paused = true;
        var theirs = _server.Game.Players[1].Fleets.First(f => f.Ships.Count > 0);
        client.Send(new FleetOrderCommand(1, theirs.ObjectId, FleetOrderKind.ToggleIntercept, -1, 0, 0, false));
        client.Send(new CheatCommand(client.Player, Cheat.GiveLotsOfMoney));
        for (var i = 0; i < 50; i++)
        {
            Frame();
        }

        Assert.Equal(hash, _server.Game.Hash());
        Assert.Empty(_server.Log);
    }

    [Fact]
    public void AFourthClientFindsTheGameFull()
    {
        var port = _server.Listen(0);
        for (var i = 0; i < 4; i++)
        {
            _clients.Add(new GameClient(Connection.Connect("127.0.0.1", port), _tables, "P" + i));
            Until(() => _clients[^1].Player >= 0 || _clients[^1].Refusal is not null, "answered");
        }

        Assert.Equal(NetText.GameFull, _clients[3].Refusal);
        Assert.Equal(3, _clients.Count(c => c.Player >= 0));
    }

    [Fact]
    public void TheHostsSpeedReachesEveryoneAndALeaverIsAnnounced()
    {
        var port = _server.Listen(0);
        foreach (var name in new[] { "Ann", "Bo" })
        {
            _clients.Add(new GameClient(Connection.Connect("127.0.0.1", port), _tables, name));
        }

        Until(() => _clients.All(c => c.Player >= 0 && c.Game is not null), "both are seated");
        var heard = _clients.ToDictionary(c => c, _ => new List<Messages.SessionNewsMessage>());
        void Listen()
        {
            foreach (var c in _clients)
            {
                heard[c].AddRange(c.TakeNews());
            }
        }

        _server.Clock.SetSpeed(2);
        Until(() => { Listen(); return _clients.All(c => heard[c].Any(n => n is { Kind: Messages.SessionNews.SpeedChanged, Value: 2 })); }, "the new speed reaches both");

        // Bo leaves: Ann and the host hear it, with the reason; Bo's empire stays.
        var bo = _clients[1];
        bo.Dispose();
        _clients.Remove(bo);
        var host = new List<Messages.SessionNewsMessage>();
        Until(() => { Listen(); host.AddRange(_server.TakeNews()); return host.Count > 0 && heard[_clients[0]].Any(n => n.Kind == Messages.SessionNews.PlayerLeft); }, "Bo's leaving is told");
        Assert.Equal(new Messages.SessionNewsMessage(Messages.SessionNews.PlayerLeft, bo.Player, (int)Messages.LeaveReason.Left), host.Single());
        Assert.Equal(3, _server.Game.Players.Count(p => !p.IsAi));
    }

    [Fact]
    public void SpeedZeroPausesOnlyWhenEveryPlayerAgrees()
    {
        var port = _server.Listen(0);
        foreach (var name in new[] { "Ann", "Bo" })
        {
            _clients.Add(new GameClient(Connection.Connect("127.0.0.1", port), _tables, name));
        }

        Until(() => _clients.All(c => c.Player >= 0 && c.Game is not null), "both are seated");
        _server.Clock.ZeroPauses = true;
        var heard = _clients.ToDictionary(c => c, _ => new List<Messages.SessionNewsMessage>());
        void Listen()
        {
            foreach (var c in _clients)
            {
                heard[c].AddRange(c.TakeNews());
            }
        }

        // Bo says no: the game goes on at its speed.
        _server.RequestPause();
        Until(() => { Listen(); return _clients.All(c => heard[c].Any(n => n.Kind == Messages.SessionNews.PauseVoteOpened)); }, "both are asked");
        _clients[0].VotePause(true);
        _clients[1].VotePause(false);
        Until(() => { Listen(); return heard[_clients[0]].Any(n => n is { Kind: Messages.SessionNews.PauseVoteClosed, Value: 0 }); }, "the vote fails");
        Assert.Equal(6, _server.Clock.Speed);

        // Both say yes: it pauses.
        _server.RequestPause();
        Until(() => { Listen(); return heard[_clients[1]].Count(n => n.Kind == Messages.SessionNews.PauseVoteOpened) == 2; }, "asked again");
        _clients.ForEach(c => c.VotePause(true));
        Until(() => _server.Clock.Speed == 0, "all agree, so it pauses");
        var tick = _server.Game.Tick;
        for (var i = 0; i < 40; i++)
        {
            Frame();
        }

        Assert.Equal(tick, _server.Game.Tick);
    }

    [Fact]
    public void AnUnansweredPauseVoteLapses()
    {
        var port = _server.Listen(0);
        _clients.Add(new GameClient(Connection.Connect("127.0.0.1", port), _tables, "Ann"));
        Until(() => _clients[0].Player >= 0 && _clients[0].Game is not null, "seated");
        _server.Clock.ZeroPauses = true;
        _server.RequestPause();
        Assert.True(_server.Voting);
        for (var i = 0; i * 50 <= GameServer.PauseVoteTime + 100; i++)
        {
            Frame();
        }

        Assert.False(_server.Voting);
        Assert.Equal(6, _server.Clock.Speed);
    }
}
