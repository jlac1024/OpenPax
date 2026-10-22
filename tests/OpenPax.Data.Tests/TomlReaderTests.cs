using OpenPax.Data.Toml;

namespace OpenPax.Data.Tests;

public class TomlReaderTests
{
    [Fact]
    public void ReadsTheSubsetThePortWrites()
    {
        var doc = TomlReader.Parse("""
            # comment
            name = "pft8 # not a comment"
            [regular]
            sheet = "pft8-regular.png"
            metrics = [11, 7]
            scale = 1.5
            on = true

            [[regular.glyph]]
            code = 32
            x = 0

            [[regular.glyph]]
            code = 33
            x = 5
            """);

        Assert.Equal("pft8 # not a comment", doc["name"]);
        var regular = (Dictionary<string, object>)doc["regular"];
        Assert.Equal("pft8-regular.png", TomlReader.String(regular, "sheet"));
        Assert.Equal(new List<object> { 11L, 7L }, regular["metrics"]);
        Assert.Equal(1.5, regular["scale"]);
        Assert.Equal(true, regular["on"]);
        Assert.Equal([32, 33], TomlReader.Tables(regular, "glyph").Select(g => TomlReader.Int(g, "code")));
    }

    [Fact]
    public void RefusesLinesItCannotRead()
    {
        Assert.Throws<FormatException>(() => TomlReader.Parse("just words"));
    }
}
