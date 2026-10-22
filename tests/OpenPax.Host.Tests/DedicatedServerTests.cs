using OpenPax.Host.Headless;
using OpenPax.Net.Client;
using OpenPax.Net.Lobby;

namespace OpenPax.Host.Tests;

/// <summary>A dedicated server in a real lobby, two players joining its room and getting their own seats through the relay.</summary>
public sealed class DedicatedServerTests
{
    [Fact]
    public async Task TwoPlayersPlayOnADedicatedServer()
    {
        var data = ImportedData.RequireFolder();
        var tables = new GameLauncher(data).Tables;
        using var lobby = new LobbyServer();
        var port = lobby.Listen(0);
        using var stop = new CancellationTokenSource();
        var pump = new Thread(() =>
        {
            var clock = System.Diagnostics.Stopwatch.StartNew();
            while (!stop.IsCancellationRequested)
            {
                lobby.Tick(clock.ElapsedMilliseconds);
                Thread.Sleep(5);
            }
        })
        { IsBackground = true };
        pump.Start();

        var options = HostOptions.Parse(["--dedicated", "127.0.0.1:" + port, "--name", "Box", "--stars", "25", "--opponents", "1"], TextWriter.Null)!; // The line above is valid.
        var log = new StringWriter();
        var server = Task.Run(() => DedicatedServer.Run(options, tables, TextWriter.Synchronized(log), stop.Token));

        var players = new[] { "Ann", "Bo" }.Select(n => new LobbyClient("127.0.0.1", port, n)).ToList();
        try
        {
            void Until(Func<bool> done, string what)
            {
                for (var i = 0; i < 1500 && !done(); i++)
                {
                    players.ForEach(p => p.Pump());
                    Thread.Sleep(10);
                }

                Assert.True(done(), what + "\n" + log);
            }

            Until(() => players.All(p => p.Rooms.Count == 1), "the server's room is listed");
            Assert.Equal("Box", players[0].Rooms[0].Host);
            players.ForEach(p => p.JoinRoom(p.Rooms[0].Id, ""));
            Until(() => players.All(p => p.Members.Count == 3), "both joined");
            players[1].Choose(new SeatChoice(2, 4, 5));
            players.ForEach(p => p.SetReady(true));
            Until(() => players.All(p => p.Starting is not null), "the server started the game");

            var clients = players.Select(p => new GameClient(p.OpenRelay(p.Starting!.Tickets[0], host: false), tables, p.Members.First(m => m.Member == p.Member).Name)).ToList(); // Not null: just waited for.
            for (var i = 0; i < 1500 && !clients.All(c => c.Game is not null); i++)
            {
                clients.ForEach(c => c.Pump());
                Thread.Sleep(10);
            }

            Assert.All(clients, c => Assert.NotNull(c.Game));
            Assert.Equal("Ann", clients[0].Game!.Players[clients[0].Player].Name); // Not null: asserted above.
            var bo = clients[1].Game!.Players[clients[1].Player]; // As above.
            Assert.Equal("Bo", bo.Name);
            Assert.Equal(tables.Species[2].Name, bo.Species.Name);
            Assert.Equal(4, bo.Flag);
            Assert.Equal(25, clients[0].Game!.Universe.Stars.Count); // As above.
            Assert.Equal(3, clients[0].Game!.Players.Count); // As above.
            clients.ForEach(c => c.Dispose());
        }
        finally
        {
            stop.Cancel();
            players.ForEach(p => p.Dispose());
        }

        Assert.Equal(0, await server);
        Assert.Contains("has started", log.ToString(), StringComparison.Ordinal);
    }
}
