using OpenPax.Data.Png;
using OpenPax.Render;
using OpenPax.Render.TrueColor;
using static OpenPax.Ui.Tests.UiGames;

namespace OpenPax.Ui.Tests;

/// <summary>OPENPAX_TEST_SHOT=folder: Species Selection saved as pictures through every pick, to look at.</summary>
public sealed class SpeciesShots
{
    [Fact]
    public void EveryPickAsPictures()
    {
        if (Environment.GetEnvironmentVariable("OPENPAX_TEST_SHOT") is not { Length: > 0 } folder)
        {
            return;
        }

        var app = NewApp();
        Click(app, 400, 110);
        Click(app, 400, 110);
        var now = 0L;
        for (var pick = 0; pick < 9; pick++)
        {
            if (pick > 0)
            {
                Click(app, 160, 255, now);
            }

            for (var t = 0; t <= 3000; t += 16)
            {
                _ = app.Tick(now + t).ToList();
                if (t % 160 == 0)
                {
                    Save(app, Path.Combine(folder, $"pick{pick}-{t:0000}.png"));
                }
            }

            now += 3016;
        }
    }

    private static void Save(UiApp app, string path)
    {
        var frame = new Framebuffer(640, 480);
        app.Draw(frame);
        var canvas = new Canvas(640, 480);
        canvas.DrawClassic(frame, app.Assets.Palette(app.PaletteIndex), 0, 0, 640, 480);
        using var file = File.Create(path);
        PngWriter.WriteRgba(file, 640, 480, System.Runtime.InteropServices.MemoryMarshal.AsBytes(canvas.Pixels.AsSpan()));
    }
}
