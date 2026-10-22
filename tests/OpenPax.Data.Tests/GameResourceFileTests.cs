using OpenPax.Data.Resources;

namespace OpenPax.Data.Tests;

public class GameResourceFileTests
{
    public static TheoryData<string> Containers => new()
    {
        "Data/GameData", "Data/HullData", "Data/PalData", "Data/ShipData", "Data/StrData",
        "Data/TechData", "Data/ViewData", "Image/Advisors.img", "Image/Combat.img",
        "Image/Empire.img", "Image/Infra.img", "Image/Main.img", "Image/Planets.img",
        "Image/Ships.img", "Image/Surface.img", "Image/Tech.img", "Image/Universe.img",
    };

    [Theory]
    [MemberData(nameof(Containers))]
    public void EveryGameContainerParses(string relative)
    {
        var path = Path.Combine(GameData.RequireFolder(), relative);
        var file = ResourceFile.Load(path);

        Assert.Single(file.Types);
        var entries = file.Entries(file.Types[0]);
        Assert.NotEmpty(entries);
        // Resources are packed back to back and the last ends at the file end.
        Assert.Equal(new FileInfo(path).Length, entries.Max(e => e.Offset + e.Length));
        Assert.All(entries, e => Assert.True(e.Length >= 4, $"resource {e.Id} too short for a class id"));
    }
}
