namespace OpenPax.Host.Tests;

/// <summary>The program carries its own window icon (tools/make_icon.sh), so it needs no imported data for it.</summary>
public sealed class WindowIconTests
{
    [Fact]
    public void TheIconIsInTheProgram()
    {
        using var stream = typeof(WindowIcon).Assembly.GetManifestResourceStream("openpax-128.rgba");
        Assert.NotNull(stream);
        Assert.Equal(128 * 128 * 4, stream.Length);

        // Opaque in the middle (the star), clear at the corner (the tile's rounding).
        var rgba = new byte[stream.Length];
        stream.ReadExactly(rgba);
        Assert.Equal(255, rgba[(((64 * 128) + 64) * 4) + 3]);
        Assert.Equal(0, rgba[3]);
    }
}
