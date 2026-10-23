using OpenPax.Data.Png;
using OpenPax.Forms.Controls;
using OpenPax.Render;
using OpenPax.Render.TrueColor;
using OpenPax.Ui.Screens;
using static OpenPax.Ui.Tests.UiGames;

namespace OpenPax.Ui.Tests;

/// <summary>The Options form as drawn: its tabs fit their panel in every text font (they once ran past it).</summary>
public sealed class OptionsLayoutTests
{
    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    public void TheTabsFitTheirPanel(int textFont)
    {
        var app = NewApp();
        app.Preferences.Set(Preferences.TextFont, textFont);
        var options = ((GameStartScreen)app.Current).OptionsPane;
        Click(app, options.Left + 5, options.Top + 5);
        var form = ((FormScreen)app.Current).Form;
        var canvas = new Canvas(1280, 960);
        _ = app.Tick(0).ToList();
        app.DrawCanvas(canvas, ClassicPlacement.For(1280, 960, 640, 480));

        var tabs = Assert.Single(form.Descendants().OfType<TabControl>());
        Assert.True(tabs.HeadersWidth <= tabs.Bounds.Width, $"tabs {tabs.HeadersWidth} wide in a panel {tabs.Bounds.Width} wide");

        // OPENPAX_TEST_SHOT=folder: the UPDATES tab saved as a picture, to look at.
        if (Environment.GetEnvironmentVariable("OPENPAX_TEST_SHOT") is { Length: > 0 } folder)
        {
            tabs.SelectedIndex = tabs.Pages.ToList().FindIndex(p => p.Header == "UPDATES");
            _ = app.Tick(0).ToList();
            app.DrawCanvas(canvas, ClassicPlacement.For(1280, 960, 640, 480));
            using var file = File.Create(Path.Combine(folder, $"options-updates-{textFont}.png"));
            PngWriter.WriteRgba(file, canvas.Width, canvas.Height, System.Runtime.InteropServices.MemoryMarshal.AsBytes(canvas.Pixels.AsSpan()));
        }
    }
}
