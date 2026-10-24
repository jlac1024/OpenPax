using OpenPax.Data.Png;
using OpenPax.Render;
using OpenPax.Render.TrueColor;
using static OpenPax.Ui.Tests.UiGames;

namespace OpenPax.Ui.Tests;

/// <summary>OPENPAX_TEST_SHOT=folder: the bridge with the pointer over the map, as the window shows it, to look at.</summary>
public sealed class BridgeShots
{
    [Fact]
    public void TheBridgeWithThePointer()
    {
        if (Environment.GetEnvironmentVariable("OPENPAX_TEST_SHOT") is not { Length: > 0 } folder)
        {
            return;
        }

        var app = InGame();
        app.CursorOnCanvas = true;
        for (var t = 0L; t < 3000; t += 16)
        {
            _ = app.Tick(t).ToList();
        }

        var placement = ClassicPlacement.For(1280, 960, 640, 480);
        foreach (var (x, y, name) in new[] { (320, 150, "map"), (100, 400, "panel") })
        {
            app.Pointer = (x, y);
            app.CanvasPointer = (x * placement.Scale + placement.X, y * placement.Scale + placement.Y);
            var frame = new Framebuffer(640, 480);
            app.Draw(frame);
            var canvas = new Canvas(1280, 960);
            app.DrawCanvas(canvas, placement);
            var whole = new Canvas(1280, 960);
            whole.Clear(Color.Black);
            whole.DrawClassic(frame, app.Assets.Palette(app.PaletteIndex), placement.X, placement.Y, placement.Width, placement.Height);
            whole.Overlay(canvas);
            using var file = File.Create(Path.Combine(folder, $"bridge-{name}.png"));
            PngWriter.WriteRgba(file, whole.Width, whole.Height, System.Runtime.InteropServices.MemoryMarshal.AsBytes(whole.Pixels.AsSpan()));
        }
    }
}
