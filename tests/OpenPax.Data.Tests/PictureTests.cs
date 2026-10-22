using System.Buffers.Binary;
using OpenPax.Data.Art;
using OpenPax.Data.Resources;

namespace OpenPax.Data.Tests;

public class PictureTests
{
    [Fact]
    public void DecodesRunsSinglesAndTransparency()
    {
        // Row 0: a run of 3 x index 7, then a single index 9.
        // Row 1: a transparent run of 2, then singles 5 and 6.
        byte[] row0 = [0xC3, 7, 9];
        byte[] row1 = [0xC2, Picture.Transparent, 5, 6];
        var body = Build(4, 2, [row0, row1]);

        var picture = Picture.Parse(body);

        Assert.Equal(4, picture.Width);
        Assert.Equal(2, picture.Height);
        Assert.Equal([7, 7, 7, 9, 1, 1, 5, 6], picture.Pixels);
    }

    [Fact]
    public void RefusesRunsPastTheRowEnd()
    {
        var body = Build(2, 1, [[0xC3, 7]]);
        Assert.Throws<ResourceFormatException>(() => Picture.Parse(body));
    }

    [Fact]
    public void RefusesZeroLengthRuns()
    {
        var body = Build(2, 1, [[0xC0, 7, 0xC2, 7]]);
        Assert.Throws<ResourceFormatException>(() => Picture.Parse(body));
    }

    [Fact]
    public void PaletteReadsRgbTriples()
    {
        var body = new byte[4 + 768];
        BinaryPrimitives.WriteInt32LittleEndian(body, ClassIds.Palette);
        body[4 + 3 * 5] = 10;
        body[4 + 3 * 5 + 1] = 20;
        body[4 + 3 * 5 + 2] = 30;

        var palette = Palette.Parse(body);

        Assert.Equal(new Rgb(10, 20, 30), palette[5]);
        Assert.Equal(new Rgb(0, 0, 0), palette[0]);
    }

    private static byte[] Build(int width, int height, byte[][] rows)
    {
        var data = rows.SelectMany(r => r).ToArray();
        var body = new byte[24 + 4 * height + data.Length];
        BinaryPrimitives.WriteInt32LittleEndian(body, ClassIds.Picture);
        BinaryPrimitives.WriteInt32LittleEndian(body.AsSpan(12), width);
        BinaryPrimitives.WriteInt32LittleEndian(body.AsSpan(16), height);
        BinaryPrimitives.WriteInt32LittleEndian(body.AsSpan(20), data.Length);
        var offset = 0;
        for (var y = 0; y < height; y++)
        {
            BinaryPrimitives.WriteInt32LittleEndian(body.AsSpan(24 + 4 * y), offset);
            offset += rows[y].Length;
        }

        data.CopyTo(body, 24 + 4 * height);
        return body;
    }
}
