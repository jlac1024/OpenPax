using OpenPax.Ui;

namespace OpenPax.Host.Tests;

/// <summary>The window opens where it was left, unless that display is gone.</summary>
public sealed class WindowPlacementTests
{
    // Two displays side by side: the main one, and one to its right.
    private static readonly (int, int, int, int)[] _two = [(0, 0, 1920, 1040), (1920, 0, 2560, 1400)];

    [Fact]
    public void ASavedPlaceOnTheSecondDisplayIsUsed()
    {
        Assert.True(WindowPlacement.OnADisplay((2400, 200, 1280, 960), _two));
        Assert.True(WindowPlacement.OnADisplay((100, 50, 1280, 960), _two));
    }

    [Fact]
    public void APlaceOnADisplayThatIsGoneIsNot()
    {
        // The second display unplugged: the window would open out of sight.
        Assert.False(WindowPlacement.OnADisplay((2400, 200, 1280, 960), [(0, 0, 1920, 1040)]));
        // Dragged nearly off the top or the side: too little left to grab.
        Assert.False(WindowPlacement.OnADisplay((100, -500, 1280, 960), _two));
        Assert.False(WindowPlacement.OnADisplay((4460, 200, 1280, 960), _two));
        Assert.False(WindowPlacement.OnADisplay((100, 100, 1280, 960), []));
    }

    [Fact]
    public void NothingSavedIsNoPlace()
    {
        Assert.Null(WindowPlacement.Saved(new Dictionary<string, int>()));
        Assert.Null(WindowPlacement.Saved(new Dictionary<string, int> { [Preferences.WindowX] = 10, [Preferences.WindowWidth] = 0 }));
        Assert.Equal((-1800, 40, 1280, 960), WindowPlacement.Saved(new Dictionary<string, int>
        {
            [Preferences.WindowX] = -1800,
            [Preferences.WindowY] = 40,
            [Preferences.WindowWidth] = 1280,
            [Preferences.WindowHeight] = 960,
        }));
    }

    [Fact]
    public void ThePlaceIsReadBeforeTheGameIsUp()
    {
        var folder = Directory.CreateTempSubdirectory("openpax-window-").FullName;
        try
        {
            var path = Path.Combine(folder, ConfigFile.FileName);
            File.WriteAllText(path, "[preferences]\nWindowX = -1800\nWindowY = 40\nWindowWidth = 1280\nWindowHeight = 960\nWindowMaximized = 1\n\n[player]\nname = \"P\"\n");
            var early = ConfigFile.EarlyPreferences(path);
            Assert.Equal(-1800, early[Preferences.WindowX]);
            Assert.Equal(1, early[Preferences.WindowMaximized]);
            Assert.Empty(ConfigFile.EarlyPreferences(Path.Combine(folder, "missing.toml")));
        }
        finally
        {
            Directory.Delete(folder, true);
        }
    }
}
