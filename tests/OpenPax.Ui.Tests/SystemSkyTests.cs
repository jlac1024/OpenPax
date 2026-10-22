using OpenPax.Ui.Widgets;
using static OpenPax.Ui.Tests.UiGames;

namespace OpenPax.Ui.Tests;

/// <summary>Each system's own sky: the same every time for a name, different between names, never the never-drawn index.</summary>
public sealed class SystemSkyTests
{
    [Fact]
    public void ASystemAlwaysGetsTheSameSkyAndAnotherADifferentOne()
    {
        var palette = NewApp().Assets.Palette(5);
        Assert.Equal(SystemSky.Seed("Thira"), SystemSky.Seed("Thira"));
        Assert.Equal(389070525u, SystemSky.Seed("Thira")); // FNV-1a: fixed for good, so a saved game's systems keep their skies
        var a = SystemSky.Paint(SystemSky.Seed("Thira"), 200, 120, palette);
        Assert.Equal(a, SystemSky.Paint(SystemSky.Seed("Thira"), 200, 120, palette));
        Assert.NotEqual(a, SystemSky.Paint(SystemSky.Seed("Homer"), 200, 120, palette));
        Assert.DoesNotContain(OpenPax.Render.Sprite.TransparentIndex, a);
    }
}
