using OpenPax.Data.Resources;
using OpenPax.Data.Views;

namespace OpenPax.Data.Tests;

public class ViewReaderTests
{
    [Fact]
    public void ReadsAWindowWithAPictureAndATitle()
    {
        var body = new List<byte>();
        void I32(int v) => body.AddRange(BitConverter.GetBytes(v));
        void Base(int id, int visible)
        {
            I32(id);
            I32(0);
            I32(0);
            I32(640);
            I32(480);
            I32(visible);
            I32(1);
        }

        I32(ViewReader.WindowClass);
        Base(10000, 1);
        body.AddRange(BitConverter.GetBytes((short)1));
        I32(8009);
        Base(10001, 0);
        I32(5050);
        body.Add(0);
        body.AddRange(BitConverter.GetBytes((short)4));
        body.AddRange("Test"u8.ToArray());

        var view = ViewReader.Parse([.. body]);

        Assert.Equal("Test", view.Title);
        Assert.Equal(2, view.Base.Visible); // a stored 1 becomes 2 on load
        Assert.Equal(new PictureDetails(5050), Assert.Single(view.Children).Details);
        Assert.Equal(10001, view.Find(10001)?.Base.Id);
    }

    [Fact]
    public void RefusesUnknownClasses()
    {
        byte[] body = [.. BitConverter.GetBytes(8000), .. new byte[28], 1, 0, .. BitConverter.GetBytes(4242), .. new byte[28]];
        Assert.Throws<ResourceFormatException>(() => ViewReader.Parse(body));
    }

    [Fact]
    public void EveryGameViewParsesExactly()
    {
        var file = ResourceFile.Load(Path.Combine(GameData.RequireFolder(), "Data", "ViewData"));
        var counts = new Dictionary<int, int>();
        foreach (var e in file.Entries(ResourceType.View))
        {
            var view = ViewReader.Parse(file.Get(ResourceType.View, e.Id).Span);
            counts[e.Id] = Count(view) - 1;
        }

        Assert.Equal(32, counts.Count);
        Assert.Equal(21, counts[10000]);
        Assert.Equal(91, counts[11300]);
        Assert.Equal(3, counts[47000]);

        // Resource 31001 holds a window whose own id is 30000.
        Assert.Equal(30000, ViewReader.Parse(file.Get(ResourceType.View, 31001).Span).Base.Id);
    }

    private static int Count(ViewNode node)
    {
        return 1 + node.Children.Sum(Count);
    }
}
