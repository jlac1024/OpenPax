using OpenPax.Render.Fonts;

namespace OpenPax.Render.Tests;

public class TextAndScaleTests
{
    private static string? FontFile()
    {
        var dir = AppContext.BaseDirectory;
        for (var d = new DirectoryInfo(dir); d is not null; d = d.Parent)
        {
            var f = Path.Combine(d.FullName, "deps", "fonts", "titillium-web", "TitilliumWeb-Regular.ttf");
            if (File.Exists(f))
            {
                return f;
            }
        }

        return null;
    }

    [Fact]
    public void TheModernFontReadsAndFills()
    {
        var path = FontFile();
        Assert.SkipWhen(path is null, "deps/fonts/titillium-web is not fetched (tools/deps.py fetch titillium-web).");
        var font = TrueTypeFont.Load(path);
        var modern = new ModernFont(font, font);
        var h = modern.Glyph('H', 40, false);
        Assert.True(h.Width > 10 && h.Height > 20);
        Assert.Contains(h.Coverage, c => c == 255);
        Assert.Contains(h.Coverage, c => c is > 0 and < 255);

        // Inside the left stem of the H it is solid; between the stems above the bar, empty.
        var row = h.Height / 4;
        Assert.Equal(0, h.Coverage[row * h.Width + h.Width / 2]);
        Assert.True(modern.Measure("HH", 40, false) > modern.Measure("H", 40, false));
        Assert.Equal(0, modern.Glyph(' ', 40, false).Width);
    }

    [Fact]
    public void AScaledFrameDrawsInTheOriginalsCoordinates()
    {
        var frame = new Framebuffer(4, 4, 2);
        Assert.Equal(8, frame.PixelWidth);
        frame.FillRect(1, 1, 1, 1, 9);
        Assert.Equal(4, frame.Pixels.Count(p => p == 9));
        Assert.Equal(9, frame.Pixels[2 * 8 + 2]);

        var sprite = new Sprite(1, 1, [5]) { Hd = new Sprite(2, 2, [6, 7, 7, Sprite.TransparentIndex]) };
        frame.Draw(sprite, 0, 0);
        Assert.Equal([6, 7], frame.Pixels[..2]);
        Assert.Equal(7, frame.Pixels[8]);
        Assert.Equal(0, frame.Pixels[9]);
    }

    [Fact]
    public void AnOutlineGrowsTheInkByItsRadius()
    {
        var dot = new byte[] { 255 };
        var ring = Outline.Grow(dot, 1, 1, 2);
        Assert.Equal(25, ring.Length);
        Assert.Equal(255, ring[2 * 5 + 2]);
        Assert.Equal(255, ring[2 * 5 + 0]);
        Assert.Equal(0, ring[0]);
    }
}
