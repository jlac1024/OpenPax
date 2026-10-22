using OpenPax.Data.Install;
using OpenPax.Data.Tables;
using OpenPax.Host.Headless;
using OpenPax.Sim.Setup;

namespace OpenPax.Sim.Tests;

/// <summary>Builds the simulation's inputs from the real GOG data files.</summary>
internal static class GameTablesForTests
{
    private static GameTables? _cached;

    public static GameTables Load()
    {
        var folder = GameData.RequireFolder();
        return _cached ??= GameTablesLoader.Load(
            Path.Combine(folder, "Data"),
            DirectionTables.FromExecutable(File.ReadAllBytes(Path.Combine(folder, GameInstall.Executable))),
            SpriteRows.Measure(OpenPax.Data.Resources.ResourceFile.Load(Path.Combine(folder, "Image", "Combat.img"))));
    }
}
