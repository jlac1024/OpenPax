using OpenPax.Render;
using static OpenPax.Ui.Tests.UiGames;

namespace OpenPax.Ui.Tests;

/// <summary>A mod's colony rating in the Sector Display's planet list (InfoPanelPane).</summary>
public sealed class ColonyRatingTests
{
    private static byte[] Frame(UiApp app)
    {
        var frame = new Framebuffer(640, 480) { Palette = app.Assets.Palette(app.PaletteIndex) };
        app.Draw(frame);
        return frame.Pixels;
    }

    [Fact]
    public void ARatingShowsOnlyWhenAModGivesOne()
    {
        var app = InGame();
        app.Command(UiCommand.SectorDisplay);
        var plain = Frame(app);
        Assert.Equal(plain, Frame(app));

        app.Session!.Game.RateColony = (_, _) => new Sim.ColonyRating(12, 340);
        var rated = Frame(app);
        Assert.NotEqual(plain, rated);
    }
}
