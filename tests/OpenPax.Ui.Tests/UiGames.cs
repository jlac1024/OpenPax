using OpenPax.Data.Tables;
using OpenPax.Host.Headless;
using OpenPax.Sim;
using OpenPax.Sim.Setup;

namespace OpenPax.Ui.Tests;

/// <summary>Drives the app through the new game screens to a started game, from the imported data.</summary>
internal static class UiGames
{
    private static GameTables? _tables;

    public static UiApp NewApp()
    {
        return new UiApp(new UiAssets(ImportedData.RequireFolder()));
    }

    /// <summary>A press and release at a point, then a frame for the screen to handle it.</summary>
    public static List<HostRequest> Click(UiApp app, int x, int y, long now = 0, int clicks = 1)
    {
        app.MouseDown(x, y, clicks, now);
        app.MouseUp(x, y);
        return [.. app.Tick(now)];
    }

    public static UiApp AtGameSetup(UiApp? from = null)
    {
        var app = from ?? NewApp();
        Click(app, 400, 110);
        Click(app, 400, 110);
        Click(app, 550, 375);
        return app;
    }

    /// <summary>Through Start, with the game made as the host makes it; the clock starts at time 0.</summary>
    public static UiApp InGame(UiApp? from = null)
    {
        var app = AtGameSetup(from);
        var choices = Click(app, 120, 470).Single().Choices!;
        var human = new HumanPlayerSetup(choices.Name, choices.Flag, choices.FleetColor, GameTablesLoader.ToSpecies(choices.Species));
        var setup = new GameSetup(choices.Seed, choices.Stars, choices.Opponents + 1, choices.Difficulty, [human]);
        app.GameStarted(NewGame.Create(setup, Tables()), 0);
        return app;
    }

    public static GameTables Tables()
    {
        var data = ImportedData.RequireFolder();
        return _tables ??= GameTablesLoader.Load(
            Path.Combine(data, "original", "Data"),
            DirectionTables.FromToml(File.ReadAllText(Path.Combine(data, "tables", DirectionTables.FileName))),
            SpriteRows.FromToml(File.ReadAllText(Path.Combine(data, "tables", SpriteRows.FileName))));
    }
}
