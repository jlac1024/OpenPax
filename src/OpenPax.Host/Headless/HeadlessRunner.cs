using OpenPax.Data.Tables;
using OpenPax.Sim;
using OpenPax.Sim.Setup;

namespace OpenPax.Host.Headless;

/// <summary>
/// Runs a game of computer players only, without a window, as fast as it
/// goes: for checking that the simulation is deterministic and for
/// replays. Nothing here reads the clock.
/// </summary>
internal static class HeadlessRunner
{
    /// <summary>Plays a new game for a number of ticks and saves the replay.</summary>
    public static int Record(string dataFolder, Replay settings, int ticks, string replayPath, TextWriter log)
    {
        var hashes = Play(dataFolder, settings, ticks);
        (settings with { Hashes = hashes }).Save(replayPath);
        log.WriteLine(string.Format(Strings.HeadlessDone, ticks, hashes.Count > 0 ? hashes[^1] : 0UL, replayPath));
        return 0;
    }

    /// <summary>Replays a saved game and checks every tick's hash.</summary>
    public static int Verify(string dataFolder, string replayPath, TextWriter log)
    {
        var replay = Replay.Load(replayPath);
        var hashes = Play(dataFolder, replay, replay.Hashes.Count);
        for (var i = 0; i < hashes.Count; i++)
        {
            if (hashes[i] != replay.Hashes[i])
            {
                log.WriteLine(string.Format(Strings.ReplayDiverged, i + 1, replay.Hashes[i], hashes[i]));
                return 1;
            }
        }

        log.WriteLine(string.Format(Strings.ReplayMatched, hashes.Count));
        return 0;
    }

    private static List<ulong> Play(string dataFolder, Replay settings, int ticks)
    {
        var directions = DirectionTables.FromToml(File.ReadAllText(Path.Combine(dataFolder, "tables", DirectionTables.FileName)));
        var sprites = SpriteRows.FromToml(File.ReadAllText(Path.Combine(dataFolder, "tables", SpriteRows.FileName)));
        var tables = GameTablesLoader.Load(Path.Combine(dataFolder, "original", "Data"), directions, sprites);
        return Play(tables, settings, ticks);
    }

    /// <summary>The replay's game played step by step, each command applied before the step it was recorded at.</summary>
    internal static List<ulong> Play(GameTables tables, Replay settings, int ticks)
    {
        var humans = settings.Humans.Select(h => new HumanPlayerSetup(h.Name, h.Flag, h.Color, tables.Species[h.Species % tables.Species.Count])).ToList();
        var setup = new GameSetup(settings.Seed, settings.Stars, settings.Players, settings.Difficulty, humans);
        var game = NewGame.Create(setup, tables, new OpenPax.Ai.ComputerPlayer());
        var byStep = settings.Commands.ToLookup(c => c.Step, c => c.Command);
        var hashes = new List<ulong>(ticks);
        for (var i = 0; i < ticks; i++)
        {
            foreach (var bytes in byStep[i])
            {
                using var stream = new MemoryStream(bytes);
                if (OpenPax.Sim.Saving.SaveFile.Read(stream, tables, null) is OpenPax.Sim.Commands.Command command)
                {
                    game.Apply(command);
                }
            }

            game.Step();
            hashes.Add(game.Hash());
        }

        return hashes;
    }

    /// <summary>A replay of a recorded game: its setup and humans, and the recording's commands and hashes.</summary>
    internal static Replay FromRecording(GameSetup setup, GameTables tables, GameRecording recording) =>
        new(setup.Seed, setup.StarCount, setup.TotalPlayers, setup.Difficulty, [.. recording.Hashes])
        {
            Humans = [.. setup.Humans.Select(h => new ReplayHuman(h.Name, h.Flag, h.FleetColor, Index(tables, h.Species)))],
            Commands = [.. recording.Commands.Select(c => (c.Step, Bytes(c.Command, tables)))],
        };

    private static int Index(GameTables tables, OpenPax.Sim.Players.Species species)
    {
        for (var i = 0; i < tables.Species.Count; i++)
        {
            if (tables.Species[i].Name == species.Name)
            {
                return i;
            }
        }

        return 0;
    }

    private static byte[] Bytes(OpenPax.Sim.Commands.Command command, GameTables tables)
    {
        using var stream = new MemoryStream();
        OpenPax.Sim.Saving.SaveFile.Write(stream, command, tables);
        return stream.ToArray();
    }
}
