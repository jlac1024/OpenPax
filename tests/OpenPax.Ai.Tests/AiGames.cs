using OpenPax.Data.Install;
using OpenPax.Data.Tables;
using OpenPax.Host.Headless;
using OpenPax.Sim;
using OpenPax.Sim.Setup;
using OpenPax.TestSupport;

namespace OpenPax.Ai.Tests;

/// <summary>Games of computer players only, from the real GOG data.</summary>
internal static class AiGames
{
    private static GameTables? _tables;

    public static GameTables Tables()
    {
        var folder = GameData.RequireFolder();
        return _tables ??= GameTablesLoader.Load(
            Path.Combine(folder, "Data"),
            DirectionTables.FromExecutable(File.ReadAllBytes(Path.Combine(folder, GameInstall.Executable))),
            SpriteRows.Measure(OpenPax.Data.Resources.ResourceFile.Load(Path.Combine(folder, "Image", "Combat.img"))));
    }

    public static Game New(uint seed = 1234, int players = 4, int difficulty = 3)
    {
        return NewGame.Create(new GameSetup(seed, 30, players, difficulty, []), Tables(), new ComputerPlayer());
    }

    public static void Run(Game game, int ticks)
    {
        for (var i = 0; i < ticks; i++)
        {
            game.Step();
        }
    }
}
