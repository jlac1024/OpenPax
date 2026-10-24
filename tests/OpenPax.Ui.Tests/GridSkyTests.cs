using OpenPax.Data.Art;
using OpenPax.Render;
using OpenPax.Ui.Widgets;

namespace OpenPax.Ui.Tests;

/// <summary>The grown planet panel's background: the art's grid on its own lines, over stars that do not repeat.</summary>
public sealed class GridSkyTests
{
    [Fact]
    public void TheGridKeepsItsLinesAndTheStarsDoNotRepeat()
    {
        var palette = new UiAssets(ImportedData.RequireFolder()).Palette(5);
        var frame = new Framebuffer(150, 900) { Palette = palette };
        new GridSky(7, 7, 8, 15, new Rgb(30, 13, 33)).Draw(frame, 0, 0, 150, 900);
        byte At(int x, int y) => frame.Pixels[(y * frame.Width) + x];

        // Down the whole height, the grid's columns are purple, and more so than the columns beside them.
        double Blue(int x) => Enumerable.Range(0, 900).Average(y => (double)palette.Colors[At(x, y)].B);
        double Red(int x) => Enumerable.Range(0, 900).Average(y => (double)palette.Colors[At(x, y)].R);
        foreach (var x in new[] { 7, 22, 142 })
        {
            Assert.True(Blue(x) > Blue(x + 1) + 10 && Blue(x) > Blue(x - 1) + 10 && Red(x) > 15, $"column {x}: blue {Blue(x)}, beside {Blue(x - 1)} and {Blue(x + 1)}");
        }

        // Rows a grid period apart differ somewhere below the art's height, unlike the repeated art.
        var differ = Enumerable.Range(430, 300).Count(y => Enumerable.Range(0, 150).Any(x => At(x, y) != At(x, y + 15)));
        Assert.True(differ > 100, $"{differ} of 300 rows differ from the row a period below");
    }
}
