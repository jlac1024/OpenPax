using OpenPax.Data;

namespace OpenPax.Modding.Tests;

public sealed class DataFilesTests
{
    [Fact]
    public void APackWinsOverTheBaseAndTheFirstPackOverTheNext()
    {
        using var temp = new TempFolder();
        temp.Write("base/art/a.png", "base");
        temp.Write("base/art/b.png", "base");
        temp.Write("low/art/b.png", "low");
        temp.Write("low/art/c.png", "low");
        temp.Write("high/art/c.png", "high");
        var files = new DataFiles(Path.Combine(temp.Path, "base"), [Path.Combine(temp.Path, "high"), Path.Combine(temp.Path, "low")]);

        Assert.Equal("base", File.ReadAllText(files.Path("art", "a.png")));
        Assert.Equal("low", File.ReadAllText(files.Path("art", "b.png")));
        Assert.Equal("high", File.ReadAllText(files.Path("art", "c.png")));
        Assert.Equal(2, files.RootIndex("art", "a.png"));
        Assert.Equal(-1, files.RootIndex("art", "none.png"));
        Assert.Equal(Path.Combine(temp.Path, "base", "art", "none.png"), files.Path("art", "none.png"));

        var under = files.FilesUnder("art");
        Assert.Equal(3, under.Count);
        Assert.Equal("high", File.ReadAllText(under["c.png"]));
    }
}
