using System.Buffers.Binary;
using OpenPax.Data.Resources;
using OpenPax.Data.Text;

namespace OpenPax.Data.Tests;

public class StringListTests
{
    [Fact]
    public void ReadsLengthPrefixedStrings()
    {
        var body = Build("AppleTalk", "", "Reduce ");

        Assert.Equal(["AppleTalk", "", "Reduce "], StringList.Parse(body));
    }

    [Fact]
    public void HighBytesAreWindows1252()
    {
        byte[] bytes = [0x41, 0x85, 0x93, 0x99, 0xE9];
        Assert.Equal("A…“™é", Windows1252.GetString(bytes));
    }

    [Fact]
    public void RefusesAStringRunningPastTheEnd()
    {
        var body = Build("abc");
        BinaryPrimitives.WriteUInt16LittleEndian(body.AsSpan(12), 50);
        Assert.Throws<ResourceFormatException>(() => StringList.Parse(body));
    }

    [Fact]
    public void EveryGameStringListParses()
    {
        var file = ResourceFile.Load(Path.Combine(GameData.RequireFolder(), "Data", "StrData"));
        var total = 0;
        foreach (var entry in file.Entries(ResourceType.StringList))
        {
            total += StringList.Parse(file.Get(ResourceType.StringList, entry.Id).Span).Count;
        }

        Assert.Equal(356, file.Entries(ResourceType.StringList).Count);
        Assert.Equal(1844, total);
        Assert.Equal("AppleTalk", StringList.Parse(file.Get(ResourceType.StringList, 70).Span)[0]);
    }

    private static byte[] Build(params string[] strings)
    {
        var data = strings.SelectMany(s => BitConverter.GetBytes((ushort)s.Length).Concat(s.Select(c => (byte)c))).ToArray();
        var body = new byte[12 + data.Length];
        BinaryPrimitives.WriteInt32LittleEndian(body, ClassIds.StringList);
        BinaryPrimitives.WriteInt32LittleEndian(body.AsSpan(4), strings.Length);
        BinaryPrimitives.WriteInt32LittleEndian(body.AsSpan(8), data.Length);
        data.CopyTo(body, 12);
        return body;
    }
}
