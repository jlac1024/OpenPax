using OpenPax.Data.Resources;
using OpenPax.Data.Text;

namespace OpenPax.Data.Tests;

public class BitmapFontTests
{
    [Fact]
    public void ReadsBothGlyphSets()
    {
        byte[] file =
        [
            .. "II"u8,
            0x41, 0x00, 0x42, 0x00,          // regular: 'A'..'B'
            2, 0, 0, 0, 1, 0, 0, 0,          // two metrics
            3, 0, 0, 0, 2, 0, 0, 0, 0xA0, 0x40, // 'A': 3 wide, 2 rows
            0, 0, 0, 0,                      // 'B': empty
            0xFF, 0xFF, 0xFF, 0xFF,          // bold absent
            9, 0, 0, 0,                      // its one metric
        ];

        var font = BitmapFont.Parse(file);

        Assert.NotNull(font.Regular);
        Assert.Null(font.Bold);
        Assert.Equal([2, 1], font.Regular.Metrics);
        var a = font.Regular.Glyphs[0];
        Assert.Equal(3, a.Width);
        Assert.Equal(2, a.Rows);
        Assert.True(a.IsSet(0, 0));
        Assert.False(a.IsSet(1, 0));
        Assert.True(a.IsSet(2, 0));
        Assert.True(a.IsSet(1, 1));
        Assert.Equal(0, font.Regular.Glyphs[1].Width);
    }

    [Fact]
    public void RefusesTrailingBytes()
    {
        byte[] file = [.. "II"u8, 0xFF, 0xFF, 0xFF, 0xFF, 0, 0, 0, 0, 0, 0, 0, 0, 0xFF, 0xFF, 0xFF, 0xFF, 0, 0, 0, 0, 7];
        Assert.Throws<ResourceFormatException>(() => BitmapFont.Parse(file));
    }

    [Theory]
    [InlineData("pft8")]
    [InlineData("pft9")]
    [InlineData("pft10")]
    [InlineData("pft11")]
    [InlineData("pft12")]
    [InlineData("pft14")]
    [InlineData("pft18")]
    public void EveryGameFontParses(string name)
    {
        var font = BitmapFont.Parse(File.ReadAllBytes(Path.Combine(GameData.RequireFolder(), "Data", name)));
        Assert.True(font.Regular is not null || font.Bold is not null);
    }
}
