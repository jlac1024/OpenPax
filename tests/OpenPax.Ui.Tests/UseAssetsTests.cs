using OpenPax.Render;
using OpenPax.Ui.Screens;
using static OpenPax.Ui.Tests.UiGames;

namespace OpenPax.Ui.Tests;

/// <summary>Another mod set's assets taken in place (docs/modding/multiplayer.md): the screen up is made again from them.</summary>
public sealed class UseAssetsTests
{
    [Theory]
    [InlineData(ScreenMode.GameStart)]
    [InlineData(ScreenMode.Options)]
    public void TheScreenUpIsMadeAgainFromTheNewAssets(ScreenMode mode)
    {
        var app = NewApp();
        if (mode != ScreenMode.GameStart)
        {
            app.Switch(mode);
        }

        var before = app.Current;
        var forms = app.Forms;
        var assets = new UiAssets(app.Assets.Files);

        app.UseAssets(assets);

        Assert.Same(assets, app.Assets);
        Assert.Equal(mode, app.Mode);
        Assert.NotSame(before, app.Current);
        Assert.NotSame(forms, app.Forms);
        _ = app.Tick(0).ToList();
        app.Draw(new Framebuffer(640, 480));
    }
}
