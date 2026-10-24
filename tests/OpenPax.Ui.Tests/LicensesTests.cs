using OpenPax.Forms.Controls;
using OpenPax.Render;
using OpenPax.Render.TrueColor;
using OpenPax.Ui.Forms;
using OpenPax.Ui.Screens;
using static OpenPax.Ui.Tests.UiGames;

namespace OpenPax.Ui.Tests;

/// <summary>The open source and attributions window, from the main menu's Licenses button.</summary>
public sealed class LicensesTests
{
    [Fact]
    public void EveryShippedComponentHasItsLicenseText()
    {
        var all = Attribution.All;
        Assert.Contains(all, a => a.Name == "OpenPax" && a.Text.Contains("GNU GENERAL PUBLIC LICENSE", StringComparison.Ordinal));
        foreach (var name in new[] { "SDL 3", "FFmpeg libraries", "FFmpeg program with x264", "Titillium Web", ".NET" })
        {
            var entry = Assert.Single(all, a => a.Name == name);
            Assert.True(entry.Text.Length > 500, $"{name} has its license's text");
            Assert.False(entry.Text.Contains('\r', StringComparison.Ordinal), name);
        }
    }

    [Fact]
    public void TheButtonOpensTheWindowAndPickingShowsEach()
    {
        var app = NewApp();
        var button = ((GameStartScreen)app.Current).LicensesPane;
        Click(app, button.Left + 10, button.Top + 5);
        Assert.Equal(ScreenMode.Licenses, app.Mode);

        var form = ((FormScreen)app.Current).Form;
        var model = (LicensesModel)form.CodeBehind!; // The window's model is its form's code-behind.
        Assert.Equal("OpenPax", model.Name);
        model.Picked = model.Names.ToList().IndexOf("SDL 3");
        Assert.Contains("zlib", model.License, StringComparison.Ordinal);
        Assert.Contains("Sam Lantinga", model.Text, StringComparison.Ordinal);

        // OPENPAX_TEST_SHOT=folder: the menu with its button and the window, to look at.
        if (Environment.GetEnvironmentVariable("OPENPAX_TEST_SHOT") is { Length: > 0 } folder)
        {
            model.Picked = 0;
            Shot(app, Path.Combine(folder, "licenses.png"));
            model.OnDone();
            Shot(app, Path.Combine(folder, "menu-licenses.png"));
        }

        model.OnDone();
        Assert.Equal(ScreenMode.GameStart, app.Mode);
    }

    private static void Shot(UiApp app, string path)
    {
        _ = app.Tick(0).ToList();
        var placement = ClassicPlacement.For(1280, 960, 640, 480);
        var frame = new Framebuffer(640, 480) { Palette = app.Assets.Palette(app.PaletteIndex) };
        app.Draw(frame);
        var canvas = new Canvas(1280, 960);
        app.DrawCanvas(canvas, placement);
        var whole = new Canvas(1280, 960);
        whole.DrawClassic(frame, app.Assets.Palette(app.PaletteIndex), placement.X, placement.Y, placement.Width, placement.Height);
        whole.Overlay(canvas);
        using var file = File.Create(path);
        OpenPax.Data.Png.PngWriter.WriteRgba(file, 1280, 960, System.Runtime.InteropServices.MemoryMarshal.AsBytes(whole.Pixels.AsSpan()));
    }
}
