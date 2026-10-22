using OpenPax.Forms.Layout;
using OpenPax.Render.TrueColor;

namespace OpenPax.Forms.Tests;

public class LayoutValueTests
{
    [Fact]
    public void MarginsReadAsXamlWritesThem()
    {
        Assert.True(Thickness.TryParse("4", out var a));
        Assert.Equal(new Thickness(4, 4, 4, 4), a);
        Assert.True(Thickness.TryParse("4,2", out var b));
        Assert.Equal(new Thickness(4, 2, 4, 2), b);
        Assert.True(Thickness.TryParse("1, 2, 3, 4", out var c));
        Assert.Equal(new Thickness(1, 2, 3, 4), c);
        Assert.False(Thickness.TryParse("1,2,3", out _));
    }

    [Fact]
    public void GridSizesReadAsXamlWritesThem()
    {
        Assert.Equal([GridLength.Auto, new GridLength(1, GridUnit.Star), new GridLength(2, GridUnit.Star), new GridLength(40, GridUnit.Pixel)], GridLength.ParseList("Auto, *, 2*, 40"));
    }

    [Fact]
    public void ColorsReadAsHex()
    {
        Assert.True(Color.TryParse("#FF8000", out var c));
        Assert.Equal((255, 128, 0, 255), (c.R, c.G, c.B, c.A));
        Assert.True(Color.TryParse("#80FF0000", out var t));
        Assert.Equal(128, t.A);
        Assert.False(Color.TryParse("red", out _));
    }

    [Fact]
    public void TranslucentColorBlends()
    {
        var half = Color.Rgb(255, 255, 255, 128);
        var over = new Color(half.Over(Color.Black.Packed));
        Assert.InRange(over.R, 126, 130);
        Assert.Equal(255, over.A);
    }
}
