using OpenPax.Ui;

namespace OpenPax.Host.Tests;

/// <summary>The player's config survives a restart, and config.json beside the game names the public lobby.</summary>
public sealed class ConfigFileTests : IDisposable
{
    private readonly string _folder = Directory.CreateTempSubdirectory("openpax-config-").FullName;

    public void Dispose() => Directory.Delete(_folder, recursive: true);

    [Fact]
    public void TheNameAndSettingsComeBackNextRun()
    {
        var data = ImportedData.RequireFolder();
        var path = Path.Combine(_folder, ConfigFile.FileName);
        var first = new UiApp(new UiAssets(data)) { PlayerName = "Jeff \"the\" Player", LobbyAddress = "lobby.example.com" };
        first.Preferences.Set(Preferences.FlagId, 7);
        ConfigFile.Save(path, first);

        var next = new UiApp(new UiAssets(data));
        ConfigFile.Load(path, data, next, TextWriter.Null);
        Assert.Equal("Jeff \"the\" Player", next.PlayerName);
        Assert.Equal("lobby.example.com", next.LobbyAddress);
        Assert.Equal(7, next.Preferences.Get(Preferences.FlagId));
    }

    [Fact]
    public void AModTurnedOnInOptionsIsOnNextRun()
    {
        var data = ImportedData.RequireFolder();
        var path = Path.Combine(_folder, ConfigFile.FileName);
        var first = new UiApp(new UiAssets(data));
        ConfigFile.Load(path, data, first, TextWriter.Null); // first run: the file is made
        first.AvailableMods.Add(new ModListing("simple-combat", "Simple Combat", "1.0", "Fights on the map"));
        first.Switch(OpenPax.Ui.Screens.ScreenMode.Options);
        var options = (OpenPax.Ui.Forms.OptionsModel)((OpenPax.Ui.Screens.FormScreen)first.Current).Form.CodeBehind!; // the screen's model is the form's code-behind
        options.ModIndex = 0;
        options.OnToggleMod();
        options.OnDone();
        _ = first.Tick(0).ToList();
        Assert.Equal(["simple-combat"], first.EnabledMods);
        ConfigFile.Save(path, first);

        // The next start reads the mods before anything else, as Program does.
        Assert.Equal(["simple-combat"], ConfigFile.EnabledMods(path));
        var next = new UiApp(new UiAssets(data));
        ConfigFile.Load(path, data, next, TextWriter.Null);
        next.EnabledMods.AddRange(ConfigFile.EnabledMods(path));
        Assert.Equal(["simple-combat"], next.EnabledMods);
        Assert.Contains("enabled = [\"simple-combat\"]", ConfigFile.Text(next), StringComparison.Ordinal);
    }

    [Fact]
    public void ANameKeptUnderLobbyBeforeOptionsIsStillRead()
    {
        var data = ImportedData.RequireFolder();
        var path = Path.Combine(_folder, ConfigFile.FileName);
        File.WriteAllText(path, "[lobby]\nname = \"Old\"\n");
        var app = new UiApp(new UiAssets(data));
        ConfigFile.Load(path, data, app, TextWriter.Null);
        Assert.Equal("Old", app.PlayerName);
    }

    [Fact]
    public void ConfigJsonNamesThePublicLobbyAndLanHosts()
    {
        var path = Path.Combine(_folder, GameConfig.FileName);
        File.WriteAllText(path, "{\n  // a comment\n  \"publicLobby\": \"wss://lobby.example.com\",\n  \"lanHosts\": [\"10.8.0.5\", \"box\"],\n}\n");
        var (lobby, hosts) = GameConfig.Read(path, TextWriter.Null);
        Assert.Equal("wss://lobby.example.com", lobby);
        Assert.Equal(["10.8.0.5", "box"], hosts);
        var (none, noHosts) = GameConfig.Read(Path.Combine(_folder, "missing.json"), TextWriter.Null);
        Assert.Null(none);
        Assert.Empty(noHosts);
    }
}
