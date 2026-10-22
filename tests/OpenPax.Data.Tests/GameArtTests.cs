using OpenPax.Data.Art;
using OpenPax.Data.Resources;

namespace OpenPax.Data.Tests;

public class GameArtTests
{
    public static TheoryData<string> ImageFiles => new()
    {
        "Advisors.img", "Combat.img", "Empire.img", "Infra.img", "Main.img",
        "Planets.img", "Ships.img", "Surface.img", "Tech.img", "Universe.img",
    };

    [Theory]
    [MemberData(nameof(ImageFiles))]
    public void EveryPictureDecodes(string name)
    {
        var file = ResourceFile.Load(Path.Combine(GameData.RequireFolder(), "Image", name));
        foreach (var entry in file.Entries(ResourceType.Picture))
        {
            var picture = Picture.Parse(file.Get(ResourceType.Picture, entry.Id).Span);
            Assert.True(picture.Width > 0 && picture.Height > 0, $"{name} {entry.Id} is empty");
        }
    }

    [Fact]
    public void EveryPaletteParses()
    {
        var file = ResourceFile.Load(Path.Combine(GameData.RequireFolder(), "Data", "PalData"));
        Assert.Equal(16, file.Entries(ResourceType.Palette).Count);
        foreach (var entry in file.Entries(ResourceType.Palette))
        {
            Palette.Parse(file.Get(ResourceType.Palette, entry.Id).Span);
        }
    }
}
