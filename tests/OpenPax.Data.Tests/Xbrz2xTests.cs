using OpenPax.Data.Art;
using OpenPax.Data.Import;
using OpenPax.Data.Png;
using OpenPax.Data.Upscale;

namespace OpenPax.Data.Tests;

public class Xbrz2xTests
{
    private const uint Black = 0xFF000000;
    private const uint White = 0xFFFFFFFF;

    [Fact]
    public void FlatPicturesStayFlatAndClearStaysClear()
    {
        var solid = Enumerable.Repeat(White, 16).ToArray();
        Assert.All(Xbrz2x.Scale(solid, 4, 4), p => Assert.Equal(White, p));
        var clear = new uint[16];
        Assert.All(Xbrz2x.Scale(clear, 4, 4), p => Assert.Equal(0u, p));
    }

    [Fact]
    public void ADiagonalEdgeIsSmoothed()
    {
        // A staircase: white above the diagonal, black below.
        const int n = 8;
        var src = new uint[n * n];
        for (var y = 0; y < n; y++)
        {
            for (var x = 0; x < n; x++)
            {
                src[y * n + x] = x > y ? White : Black;
            }
        }

        var up = Xbrz2x.Scale(src, n, n);
        Assert.Equal(n * n * 4, up.Length);

        // Nearest doubling has only the two colors; the scaler blends in between along the edge.
        Assert.Contains(up, p => p != Black && p != White);
    }

    [Fact]
    public void UpscaledPixelsUseOnlyIndicesFoundAroundThem()
    {
        var palette = Enumerable.Range(0, 256).Select(i => new Rgb((byte)i, (byte)i, (byte)i)).ToList();
        var pixels = new byte[] { 10, 10, 200, 10, 200, 200, 200, 200, 200 };
        var image = new IndexedImage(3, 3, pixels, palette, []);
        var up = Xbrz2x.Scale(UpscaleImporter.ToRgba(image), 3, 3);
        var indices = UpscaleImporter.ToPalette(up, image);
        Assert.Equal(36, indices.Length);
        Assert.All(indices, i => Assert.Contains(i, new byte[] { 10, 200 }));
    }
}
