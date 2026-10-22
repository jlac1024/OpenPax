using OpenPax.Data.Resources;
using OpenPax.Data.Tables;

namespace OpenPax.Data.Tests;

public class HullTests
{
    [Fact]
    public void ReadsTheThreeValues()
    {
        byte[] body = [0xC4, 0x1B, 0, 0, 100, 0, 0, 0, 0xF4, 0x01, 0, 0, 0xEE, 0x02, 0, 0];

        Assert.Equal(new Hull(2000, 100, 500, 750), Hull.Parse(2000, body));
    }

    [Fact]
    public void RefusesOtherClasses()
    {
        Assert.Throws<ResourceFormatException>(() => Hull.Parse(1, new byte[16]));
    }

    [Fact]
    public void GameHullsMatchTheKnownTable()
    {
        var file = ResourceFile.Load(Path.Combine(GameData.RequireFolder(), "Data", "HullData"));
        var hulls = file.Entries(ResourceType.Hull).Select(e => Hull.Parse(e.Id, file.Get(ResourceType.Hull, e.Id).Span)).ToArray();

        Assert.Equal(
            [
                new Hull(2000, 100, 500, 750),
                new Hull(2001, 400, 1000, 1500),
                new Hull(2002, 200, 2000, 1500),
                new Hull(2003, 1000, 2000, 3000),
                new Hull(2004, 2000, 4000, 6000),
                new Hull(2005, 1000, 4000, 4000),
            ],
            hulls);
    }
}
