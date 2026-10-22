using OpenPax.Data.Art;
using OpenPax.Data.Png;

namespace OpenPax.Render.Tests;

public sealed class BitmapTextTests : IDisposable
{
    private readonly string _folder = Directory.CreateTempSubdirectory("openpax-font").FullName;

    public void Dispose()
    {
        Directory.Delete(_folder, true);
    }

    [Fact]
    public void GlyphsSitOnTheBaselineAndAdvanceByWidthLessOne()
    {
        var frame = new Framebuffer(32, 8);
        LoadFont().Draw(frame, "AB", 10, 5, 0, 0, 7, 0);

        // Ascent 2: rows 3 and 4. A covers x 10..12, B starts at 12 and covers 12..15.
        Assert.Equal([0, 0, 7, 7, 7, 7, 7, 7, 0], Row(frame, 3, 8, 17));
        Assert.Equal(0, frame.Pixels[2 * 32 + 11]);
        Assert.Equal(0, frame.Pixels[5 * 32 + 11]);
    }

    [Fact]
    public void CenteredTextMovesLeftByHalfItsWidth()
    {
        var frame = new Framebuffer(32, 8);
        LoadFont().Draw(frame, "AB", 10, 5, 0, 1, 7, 0);

        // Width (3 - 1) + (4 - 1) = 5, so the text starts at 10 - 2.
        Assert.Equal([0, 7, 7, 7, 7, 7, 7, 0], Row(frame, 3, 7, 15));
    }

    [Fact]
    public void ShadowIsDrawnFirstOneDownAndRight()
    {
        var frame = new Framebuffer(32, 8);
        LoadFont().Draw(frame, "A", 10, 5, 4, 0, 7, 9);

        Assert.Equal([7, 7, 7, 9], Row(frame, 4, 10, 14));
        Assert.Equal([0, 9, 9, 9], Row(frame, 5, 10, 14));
    }

    [Fact]
    public void SpritesSkipIndexOneAndClip()
    {
        var frame = new Framebuffer(4, 1);
        frame.Draw(new Sprite(3, 1, [5, 1, 6]), 2, 0);

        Assert.Equal([0, 0, 5, 0], frame.Pixels);
    }

    private static int[] Row(Framebuffer frame, int y, int from, int to)
    {
        return [.. frame.Pixels.Skip(y * frame.Width + from).Take(to - from).Select(p => (int)p)];
    }

    private BitmapText LoadFont()
    {
        // A 7x2 sheet of ink: A is 3 wide at x 0, B is 4 wide at x 3.
        var palette = Enumerable.Repeat(new Rgb(0, 0, 0), 256).ToArray();
        using (var sheet = File.Create(Path.Combine(_folder, "test-regular.png")))
        {
            PngWriter.Write(sheet, new IndexedImage(7, 2, Enumerable.Repeat((byte)1, 14).ToArray(), palette, [0]));
        }

        var toml = Path.Combine(_folder, "test.toml");
        File.WriteAllText(toml, """
            [regular]
            sheet = "test-regular.png"
            first = 65
            last = 66
            metrics = [2, 2]

            [[regular.glyph]]
            code = 65
            x = 0
            y = 0
            width = 3
            rows = 2

            [[regular.glyph]]
            code = 66
            x = 3
            y = 0
            width = 4
            rows = 2
            """);
        return BitmapText.Load(toml);
    }
}
