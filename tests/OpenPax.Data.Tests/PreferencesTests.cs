using OpenPax.Data.Tables;

namespace OpenPax.Data.Tests;

public class PreferencesTests
{
    [Fact]
    public void ReadsAllThreeValueTypes()
    {
        byte[] file =
        [
            .. "II"u8, 3, 0, 0, 0,
            1, 0, (byte)'a', 3, 0, 0, 0, 7, 0, 0, 0,
            1, 0, (byte)'b', 2, 0, 0, 0, 2, 0, (byte)'h', (byte)'i',
            1, 0, (byte)'c', 8, 0, 0, 0, 1,
        ];

        var prefs = Preferences.Parse(file);

        Assert.Equal(["a", "b", "c"], prefs.Select(p => p.Key));
        Assert.Equal(7, prefs[0].Value);
        Assert.Equal("hi", prefs[1].Value);
        Assert.Equal(true, prefs[2].Value);
    }

    [Fact]
    public void TheGogPreferencesParse()
    {
        var prefs = Preferences.Parse(File.ReadAllBytes(Path.Combine(GameData.RequireFolder(), "Data", "PrefData")));

        Assert.Equal(17, prefs.Count);
        Assert.Contains(new KeyValuePair<string, object>("GameSpeed", 3), prefs);
        Assert.Contains(new KeyValuePair<string, object>("PlayMovies", true), prefs);
    }
}
