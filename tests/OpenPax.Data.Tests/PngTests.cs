using OpenPax.Data.Art;
using OpenPax.Data.Png;

namespace OpenPax.Data.Tests;

public class PngTests
{
    [Fact]
    public void Crc32MatchesTheStandardCheckValue()
    {
        Assert.Equal(0xCBF43926u, Crc32.Compute("123456789"u8, []));
    }

    [Fact]
    public void IndexedImagesRoundTrip()
    {
        var palette = Enumerable.Range(0, 256).Select(i => new Rgb((byte)i, (byte)(255 - i), (byte)(i * 7))).ToArray();
        var pixels = Enumerable.Range(0, 5 * 3).Select(i => (byte)(i * 17)).ToArray();
        var image = new IndexedImage(5, 3, pixels, palette, [255, 0]);

        using var stream = new MemoryStream();
        PngWriter.Write(stream, image);
        var back = PngReader.Read(stream.ToArray());

        Assert.Equal(5, back.Width);
        Assert.Equal(3, back.Height);
        Assert.Equal(pixels, back.Pixels);
        Assert.Equal(palette, back.Palette);
        Assert.Equal([255, 0], back.Alpha);
    }

    [Fact]
    public void RejectsCorruptChunks()
    {
        var image = new IndexedImage(1, 1, [0], [new Rgb(1, 2, 3)], []);
        using var stream = new MemoryStream();
        PngWriter.Write(stream, image);
        var bytes = stream.ToArray();
        bytes[20] ^= 0xFF; // inside IHDR, so its checksum no longer matches

        Assert.Throws<Resources.ResourceFormatException>(() => PngReader.Read(bytes));
    }
}
