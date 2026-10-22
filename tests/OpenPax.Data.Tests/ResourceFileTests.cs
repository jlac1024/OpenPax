using OpenPax.Data.Resources;

namespace OpenPax.Data.Tests;

public class ResourceFileTests
{
    [Fact]
    public void TypeNamesRoundTripAndMatchDiskOrder()
    {
        var race = ResourceType.FromName("RACE");
        Assert.Equal(0x52414345u, race.Value);
        Assert.Equal("RACE", race.ToString());
        // On disk the little-endian integer reads as "ECAR".
        Assert.Equal("ECAR"u8.ToArray(), BitConverter.GetBytes(race.Value));
    }

    [Fact]
    public void FindsResourcesByIdAndIndexWithDerivedLengths()
    {
        var data = new ContainerBuilder()
            .Add("PPIC", 10, 1, 2, 3)
            .Add("PPIC", 20, 4)
            .Add("PPIC", 35, 5, 6)
            .Build();

        var file = ResourceFile.Parse("test", data);

        Assert.Equal([ResourceType.Picture], file.Types);
        Assert.Equal([10, 20, 35], file.Entries(ResourceType.Picture).Select(e => e.Id));
        Assert.Equal([3, 1, 2], file.Entries(ResourceType.Picture).Select(e => e.Length));
        Assert.Equal([4], file.Get(ResourceType.Picture, 20).ToArray());
        Assert.Equal([5, 6], file.GetByIndex(ResourceType.Picture, 2).ToArray());
        Assert.False(file.TryGet(ResourceType.Picture, 21, out _));
        Assert.Throws<KeyNotFoundException>(() => file.Get(ResourceType.Picture, 11));
    }

    [Fact]
    public void ReadsSeveralTypesInOneFile()
    {
        // No GOG file has more than one type, but the original's reader
        // allows it, so ours does too.
        var data = new ContainerBuilder()
            .Add("STR#", 1, 9)
            .Add("CPLT", 1, 7, 7)
            .Build();

        var file = ResourceFile.Parse("test", data);

        Assert.Equal([ResourceType.StringList, ResourceType.Palette], file.Types);
        Assert.Equal([9], file.Get(ResourceType.StringList, 1).ToArray());
        Assert.Equal([7, 7], file.Get(ResourceType.Palette, 1).ToArray());
        Assert.Empty(file.Entries(ResourceType.Tech));
    }

    [Fact]
    public void DuplicateIdsResolveToTheFirst()
    {
        var data = new ContainerBuilder()
            .Add("PPIC", 1, 1)
            .Add("PPIC", 7, 2)
            .Add("PPIC", 7, 3)
            .Add("PPIC", 9, 4)
            .Build();

        var file = ResourceFile.Parse("test", data);

        Assert.Equal([2], file.Get(ResourceType.Picture, 7).ToArray());
        Assert.Equal([3], file.GetByIndex(ResourceType.Picture, 2).ToArray());
        Assert.Equal([4], file.Get(ResourceType.Picture, 9).ToArray());
    }

    [Fact]
    public void RefusesIdsOutOfOrder()
    {
        var data = new ContainerBuilder().Add("TECH", 5, 1).Add("TECH", 4, 2).Build();
        Assert.Throws<ResourceFormatException>(() => ResourceFile.Parse("test", data));
    }

    [Fact]
    public void RefusesOtherIIFiles()
    {
        // Font header: first and last character codes where the type count would be.
        byte[] font = [0x49, 0x49, 0x20, 0x00, 0xFE, 0x00, 0x0B, 0x00];
        Assert.Throws<ResourceFormatException>(() => ResourceFile.Parse("pft8", font));
        Assert.Throws<ResourceFormatException>(() => ResourceFile.Parse("empty", []));
    }

    [Fact]
    public void RefusesTruncatedDirectory()
    {
        var data = new ContainerBuilder().Add("TECH", 1, 1).Add("TECH", 2, 2).Build();
        Assert.Throws<ResourceFormatException>(() => ResourceFile.Parse("test", data[..20]));
    }

    [Fact]
    public void RefusesOffsetsIntoTheDirectory()
    {
        var data = new ContainerBuilder().Add("TECH", 1, 1).Build();
        data[18] = 2; // first offset now points inside the header
        Assert.Throws<ResourceFormatException>(() => ResourceFile.Parse("test", data));
    }
}
