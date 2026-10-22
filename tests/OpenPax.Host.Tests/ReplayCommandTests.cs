using OpenPax.Host.Headless;
using OpenPax.Sim;
using OpenPax.Sim.Commands;
using OpenPax.Sim.Players;
using OpenPax.Sim.Setup;

namespace OpenPax.Host.Tests;

/// <summary>A game with humans and their commands, recorded, saved, loaded and played again to the same hash at every step.</summary>
public class ReplayCommandTests
{
    [Fact]
    public void ARecordedGameReplaysStepForStep()
    {
        var tables = new GameLauncher(ImportedData.RequireFolder()).Tables;
        var setup = new GameSetup(4242, 30, 4, 3, [new HumanPlayerSetup("Ann", 1, 2, tables.Species[0]), new HumanPlayerSetup("Bo", 3, 4, tables.Species[2])]);
        var game = NewGame.Create(setup, tables, new OpenPax.Ai.ComputerPlayer());
        var recording = game.StartRecording();
        for (var i = 0; i < 600; i++)
        {
            if (i == 50)
            {
                game.Apply(new FleetOrderCommand(0, game.Players[0].Fleets.First(f => f.Ships.Count > 0).ObjectId, FleetOrderKind.ToggleIntercept, -1, 0, 0, false));
                game.Apply(new SpendingCommand(1, 1, 60));
            }

            if (i == 300)
            {
                game.Apply(new AllocationCommand(0, 0, 50));
                game.Apply(new CheatCommand(1, Cheat.GiveLotsOfMoney));
            }

            game.Step();
        }

        var path = Path.Combine(Path.GetTempPath(), "openpax-replay-" + Environment.ProcessId + ".toml");
        try
        {
            HeadlessRunner.FromRecording(setup, tables, recording).Save(path);
            var loaded = Replay.Load(path);
            Assert.Equal(2, loaded.Humans.Count);
            Assert.Equal("Bo", loaded.Humans[1].Name);
            Assert.Equal(4, loaded.Commands.Count);
            Assert.Equal(recording.Hashes, HeadlessRunner.Play(tables, loaded, loaded.Hashes.Count));

            // Without the commands the game goes another way.
            var without = HeadlessRunner.Play(tables, new Replay(loaded.Seed, loaded.Stars, loaded.Players, loaded.Difficulty, loaded.Hashes) { Humans = loaded.Humans }, loaded.Hashes.Count);
            Assert.NotEqual(recording.Hashes[^1], without[^1]);
        }
        finally
        {
            File.Delete(path);
        }
    }
}
