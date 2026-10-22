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
