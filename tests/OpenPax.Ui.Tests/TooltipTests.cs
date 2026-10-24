using OpenPax.Data.Png;
using OpenPax.Render;
using OpenPax.Render.TrueColor;
using static OpenPax.Ui.Tests.UiGames;

namespace OpenPax.Ui.Tests;

/// <summary>The economy and planet screens say what their figures are for when the pointer rests on them.</summary>
public sealed class TooltipTests
{
    [Fact]
    public void TheBudgetAndThePlanetRowsExplainThemselves()
    {
        var app = InGame();
        app.Command(UiCommand.DomesticPolicy);
        var screen = app.Current;
        var subsidy = screen.Window.Get(17026);
        Assert.Equal(UiText.TipSubsidyCost, screen.TooltipAt(subsidy.Left + 5, subsidy.Top + 5));
        Assert.Equal(UiText.TipResearchSpending, screen.TooltipAt(40, 78));

        // The Population caption, and a planet column's figure on its row.
        Assert.Equal(UiText.TipPopulation, screen.TooltipAt(40, 333));
        Assert.Equal(UiText.TipPopulation, screen.TooltipAt(300, 333));
        Assert.Null(screen.TooltipAt(320, 100));
    }

    [Fact]
    public void ThePlanetsFiguresExplainThemselves()
    {
        var app = InGame();
        app.Command(UiCommand.PlanetarySurface);
        var screen = app.Current;
        var stats = screen.Window.Get(14014);
        Assert.Equal(UiText.TipColonyResearch, screen.TooltipAt(stats.Left + 20, stats.Top + 82));
        Assert.Equal(UiText.TipColonySubsidy, screen.TooltipAt(300, 410));
    }

    [Fact]
    public void ATooltipShowsOnlyOnceThePointerRests()
    {
        var app = InGame();
        app.Command(UiCommand.DomesticPolicy);
        var placement = ClassicPlacement.For(1280, 960, 640, 480);
        var subsidy = app.Current.Window.Get(17026);
        var (x, y) = (subsidy.Left + 5, subsidy.Top + 5);
        app.CanvasPointer = ((x * placement.Scale) + placement.X, (y * placement.Scale) + placement.Y);
        app.MouseMove(x, y, 1000);
        Canvas Shown(long now)
        {
            _ = app.Tick(now).ToList();
            var canvas = new Canvas(1280, 960);
            app.DrawCanvas(canvas, placement);
            return canvas;
        }

        // Nothing yet after a tenth of a second; after the delay, the box.
        var early = Shown(1100);
        var shown = Shown(1700);
        Assert.True(shown.Pixels.Where((p, i) => p != early.Pixels[i]).Count() > 2000);

        // OPENPAX_TEST_SHOT=folder: the tooltip over the screen, to look at.
        if (Environment.GetEnvironmentVariable("OPENPAX_TEST_SHOT") is { Length: > 0 } folder)
        {
            var frame = new Framebuffer(640, 480) { Palette = app.Assets.Palette(app.PaletteIndex) };
            app.Draw(frame);
            var whole = new Canvas(1280, 960);
            whole.Clear(Color.Black);
            whole.DrawClassic(frame, app.Assets.Palette(app.PaletteIndex), placement.X, placement.Y, placement.Width, placement.Height);
            whole.Overlay(shown);
            using var file = File.Create(Path.Combine(folder, "tooltip.png"));
            PngWriter.WriteRgba(file, whole.Width, whole.Height, System.Runtime.InteropServices.MemoryMarshal.AsBytes(whole.Pixels.AsSpan()));
        }
    }
}
