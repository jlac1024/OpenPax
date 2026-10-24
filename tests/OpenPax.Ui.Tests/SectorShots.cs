using OpenPax.Data.Png;
using OpenPax.Render;
using OpenPax.Render.TrueColor;
using static OpenPax.Ui.Tests.UiGames;

namespace OpenPax.Ui.Tests;

/// <summary>OPENPAX_TEST_SHOT=folder: Sector Display grown for a tall and a wide window, to look at.</summary>
public sealed class SectorShots
{
    [Fact]
    public void TheMainMenu()
    {
        if (Environment.GetEnvironmentVariable("OPENPAX_TEST_SHOT") is not { Length: > 0 } folder)
        {
            return;
        }

        var app = NewApp();
        var placement = ClassicPlacement.For(1280, 960, 640, 480);
        _ = app.Tick(0).ToList();
        var frame = new Framebuffer(640, 480) { Palette = app.Assets.Palette(app.PaletteIndex) };
        app.Draw(frame);
        var canvas = new Canvas(1280, 960);
        app.DrawCanvas(canvas, placement);
        var whole = new Canvas(1280, 960);
        whole.DrawClassic(frame, app.Assets.Palette(app.PaletteIndex), placement.X, placement.Y, placement.Width, placement.Height);
        whole.Overlay(canvas);
        using var file = File.Create(Path.Combine(folder, "main-menu.png"));
        PngWriter.WriteRgba(file, 1280, 960, System.Runtime.InteropServices.MemoryMarshal.AsBytes(whole.Pixels.AsSpan()));
    }

    [Theory]
    [InlineData(1280, 961, 16)]
    [InlineData(1280, 962, 14)]
    public void OtherScreens(int windowWidth, int windowHeight, int screen)
    {
        if (Environment.GetEnvironmentVariable("OPENPAX_TEST_SHOT") is not { Length: > 0 } folder)
        {
            return;
        }

        var app = InGame();
        app.Command(screen == 16 ? UiCommand.DomesticPolicy : UiCommand.PlanetarySurface);
        app.FitWindow(windowWidth, windowHeight);
        for (var t = 0L; t < 500; t += 16)
        {
            _ = app.Tick(t).ToList();
        }

        var frame = new Framebuffer(app.ClassicWidth, app.ClassicHeight) { Palette = app.Assets.Palette(app.PaletteIndex) };
        app.Draw(frame);
        var canvas = new Canvas(app.ClassicWidth, app.ClassicHeight);
        canvas.DrawClassic(frame, app.Assets.Palette(app.PaletteIndex), 0, 0, app.ClassicWidth, app.ClassicHeight);
        using var file = File.Create(Path.Combine(folder, $"screen-{screen}.png"));
        PngWriter.WriteRgba(file, canvas.Width, canvas.Height, System.Runtime.InteropServices.MemoryMarshal.AsBytes(canvas.Pixels.AsSpan()));
    }

    [Theory]
    [InlineData(1280, 2000)]
    [InlineData(2560, 1080)]
    [InlineData(1280, 960)]
    [InlineData(2000, 1100)]
    public void TheSectorScreenGrown(int windowWidth, int windowHeight)
    {
        if (Environment.GetEnvironmentVariable("OPENPAX_TEST_SHOT") is not { Length: > 0 } folder)
        {
            return;
        }

        var app = InGame();
        app.Command(UiCommand.SectorDisplay);
        app.FitWindow(windowWidth, windowHeight);
        for (var t = 0L; t < 500; t += 16)
        {
            _ = app.Tick(t).ToList();
        }

        // At the window's own scale, with the upscaled art at 2x, as the game draws it.
        var scale = app.FrameScale;
        app.Assets.Upscaled = scale % 2 == 0;
        var frame = new Framebuffer(app.ClassicWidth, app.ClassicHeight, scale) { Palette = app.Assets.Palette(app.PaletteIndex) };
        app.Draw(frame);
        var canvas = new Canvas(app.ClassicWidth * scale, app.ClassicHeight * scale);
        canvas.DrawClassic(frame, app.Assets.Palette(app.PaletteIndex), 0, 0, app.ClassicWidth * scale, app.ClassicHeight * scale);
        using var file = File.Create(Path.Combine(folder, $"sector-{windowWidth}x{windowHeight}.png"));
        PngWriter.WriteRgba(file, canvas.Width, canvas.Height, System.Runtime.InteropServices.MemoryMarshal.AsBytes(canvas.Pixels.AsSpan()));
    }
}
