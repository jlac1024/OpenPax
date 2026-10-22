using System.Diagnostics;
using OpenPax.Net.Lobby;
using OpenPax.Net.Server;
using OpenPax.Sim;
using OpenPax.Sim.Setup;
using OpenPax.Sim.Time;

namespace OpenPax.Host.Headless;

/// <summary>
/// A game server with no player of its own (docs/netcode/modern.md): it
/// joins a lobby under its name, opens a room with the galaxy given on its
/// command line, starts once enough members are in and all are ready,
/// makes the game from their choices, serves it through the lobby's relay,
/// and stops when the game is over or everyone has left.
/// </summary>
internal static class DedicatedServer
{
    private const int StepMilliseconds = 5;
    private const int WaitForLobby = 10_000;
    private const int EmptyGrace = 60_000;

    public static int Run(HostOptions options, GameTables tables, TextWriter log, CancellationToken stop, Ui.StringTable? strings = null, OpenPax.Modding.ModHost? mods = null)
    {
        if (options.Dedicated is not { } address || !LobbyAddress.TryParse(address, out var lobbyAddress))
        {
            log.WriteLine(string.Format(Strings.DedicatedBadAddress, options.Dedicated));
            return 2;
        }

        mods ??= OpenPax.Modding.ModHost.None;
        using var lobby = new LobbyClient(lobbyAddress, options.ServerName, mods.Fingerprint);

        // The room's members fetch its mods from here, through the lobby (docs/modding/multiplayer.md).
        var hosted = mods;
        lobby.PackageFor = key => hosted.Mods.FirstOrDefault(m => m.Key.ToString() == key) is { } mod ? OpenPax.Modding.ModPackage.Pack(mod.Folder) : null;
        var clock = Stopwatch.StartNew();
        if (!Until(() => lobby.Member > 0 || lobby.Refusal is not null || !lobby.Connected, lobby, clock, WaitForLobby, stop) || lobby.Member == 0)
        {
            log.WriteLine(string.Format(Strings.DedicatedNoLobby, lobby.Refusal ?? Strings.DedicatedNoAnswer));
            return 1;
        }

        lobby.CreateRoom(options.RoomName ?? options.ServerName + Strings.DedicatedRoomSuffix, options.RoomPassword, LobbyServer.MaxPlayers);
        Until(() => lobby.Room is not null, lobby, clock, WaitForLobby, stop);
        lobby.SetSettings(new RoomSettings(options.Stars, options.Opponents, options.Difficulty));
        log.WriteLine(string.Format(Strings.DedicatedOpen, lobby.Room?.Name, options.MinPlayers));

        // Start once enough members are in and every one is ready.
        var asked = false;
        while (lobby.Starting is null)
        {
            if (stop.IsCancellationRequested || !lobby.Connected)
            {
                return 1;
            }

            lobby.Pump();
            var members = lobby.Members.Skip(1).ToList();
            if (!asked && members.Count >= options.MinPlayers && members.All(m => m.Ready))
            {
                lobby.StartGame();
                asked = true;
            }
            else if (asked && !members.All(m => m.Ready))
            {
                asked = false;
            }

            Thread.Sleep(20);
        }

        var (game, setup) = MakeGame(options, tables, lobby.Members.Skip(1).ToList(), clock, strings);
        mods.NewGame(game);
        var recording = options.ReplayOutGiven ? game.StartRecording() : null;
        using var server = new GameServer(game, new GameClock(clock.ElapsedMilliseconds), host: -1);
        foreach (var ticket in lobby.Starting.Tickets)
        {
            server.Accept(lobby.OpenRelay(ticket, host: true));
        }

        log.WriteLine(string.Format(Strings.DedicatedStarted, game.Players.Count(p => !p.IsAi), game.Universe.Stars.Count));
        var started = clock.ElapsedMilliseconds;
        while (!stop.IsCancellationRequested)
        {
            var now = clock.ElapsedMilliseconds;
            server.Tick(now);
            lobby.Pump();
            if (game.Over)
            {
                log.WriteLine(string.Format(Strings.DedicatedOver, game.Tick));
                break;
            }

            if (now - started > EmptyGrace && !server.Seated.Any())
            {
                log.WriteLine(Strings.DedicatedEmpty);
                break;
            }

            Thread.Sleep(StepMilliseconds);
        }

        if (recording is not null)
        {
            HeadlessRunner.FromRecording(setup, tables, recording).Save(options.ReplayOut);
            log.WriteLine(string.Format(Strings.DedicatedReplay, options.ReplayOut, recording.Hashes.Count));
        }

        return 0;
    }

    /// <summary>The members in seat order as they chose (a designed species as sent, when the game's strings are given to read it), a choice not made being the stock species in turn and the seat's number as flag and color.</summary>
    internal static (Game Game, GameSetup Setup) MakeGame(HostOptions options, GameTables tables, IReadOnlyList<RoomMember> members, Stopwatch clock, Ui.StringTable? strings = null)
    {
        var humans = members.Select((m, i) => new HumanPlayerSetup(
            m.Name,
            m.Choice.Flag >= 0 ? m.Choice.Flag % Ui.Widgets.FlagPane.Flags : i,
            m.Choice.Color >= 0 ? m.Choice.Color % Ui.Widgets.FleetColorPane.Colors : i,
            (strings is null ? null : Ui.SeatSpecies.Unpack(m.Choice.Custom, strings)) is { } designed
                ? GameTablesLoader.ToSpecies(designed)
                : tables.Species[(m.Choice.Species >= 0 ? m.Choice.Species : i) % tables.Species.Count])).ToList();
        var seed = (uint)((clock.ElapsedTicks ^ Environment.TickCount64) % 25000);
        var setup = new GameSetup(seed, options.Stars, options.Opponents + humans.Count, options.Difficulty, humans);
        return (NewGame.Create(setup, tables, new OpenPax.Ai.ComputerPlayer()), setup);
    }

    private static bool Until(Func<bool> done, LobbyClient lobby, Stopwatch clock, int wait, CancellationToken stop)
    {
        var end = clock.ElapsedMilliseconds + wait;
        while (!done() && clock.ElapsedMilliseconds < end && !stop.IsCancellationRequested)
        {
            lobby.Pump();
            Thread.Sleep(20);
        }

        return done();
    }
}
