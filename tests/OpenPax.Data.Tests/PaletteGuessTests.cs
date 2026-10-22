using OpenPax.Data.Art;
using OpenPax.Data.Import;

namespace OpenPax.Data.Tests;

public class PaletteGuessTests
{
    // Palette 0: index i is a gray ramp, so a gradient picture is smooth.
    // Palette 1: the same colors shuffled, so the same picture is noise.
    private static readonly Palette[] _palettes = [Ramp(), Shuffled()];

    [Fact]
    public void PrefersThePaletteThatMakesTheImageSmooth()
    {
        var picture = Gradient(32, 32);

        var m = PaletteGuess.Measure(picture, _palettes);

        Assert.Equal(0, m.Best());
        Assert.True(m.Score(1) > 2 * m.Score(0));
    }

    [Fact]
    public void AGroupDecidesForPicturesThatCannotBeMeasured()
    {
        var gradient = PaletteGuess.Measure(Gradient(32, 32), _palettes);
        var flat = PaletteGuess.Measure(new Picture(0, 0, 8, 8, Enumerable.Repeat((byte)40, 64).ToArray()), _palettes);

        var group = PaletteGuess.ChooseForGroup([gradient, flat], _palettes.Length);

        Assert.Equal(0, group.Palette);
        Assert.Equal(0, PaletteGuess.ChooseForPicture(flat, group));
    }

    [Fact]
    public void FullyTransparentPicturesGetTheDefault()
    {
        var empty = PaletteGuess.Measure(new Picture(0, 0, 4, 4, Enumerable.Repeat(Picture.Transparent, 16).ToArray()), _palettes);

        var group = PaletteGuess.ChooseForGroup([empty], _palettes.Length);

        Assert.Equal(0, empty.Pairs);
        Assert.False(double.IsFinite(group.Margin));
    }

    private static Picture Gradient(int w, int h)
    {
        var px = new byte[w * h];
        for (var y = 0; y < h; y++)
        {
            for (var x = 0; x < w; x++)
            {
                px[y * w + x] = (byte)(2 + (x + y) * 3);
            }
        }

        return new Picture(0, 0, w, h, px);
    }

    private static Palette Ramp()
    {
        return new Palette([.. Enumerable.Range(0, 256).Select(i => new Rgb((byte)i, (byte)i, (byte)i))]);
    }

    private static Palette Shuffled()
    {
        // Multiplying by an odd number permutes 0..255 and scatters neighbors.
        return new Palette([.. Enumerable.Range(0, 256).Select(i => (byte)(i * 151 % 256)).Select(v => new Rgb(v, v, v))]);
    }
}
