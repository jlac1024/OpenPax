namespace OpenPax.Sim.Tests;

public class MsvcSortTests
{
    private static int ByKey((int Key, char Name) a, (int Key, char Name) b) => a.Key.CompareTo(b.Key);

    [Fact]
    public void ShortRangesPutEqualKeysInSelectionSortOrder()
    {
        (int, char)[] items = [(1, 'a'), (1, 'b'), (0, 'c')];

        MsvcSort.Sort<(int Key, char Name)>(items, ByKey);

        // Traced by hand through the original's selection sort.
        Assert.Equal("cba", new string(items.Select(i => i.Item2).ToArray()));
    }

    [Fact]
    public void LongRangesPartitionThenSelectionSort()
    {
        (int, char)[] items = [(5, 'a'), (1, 'b'), (5, 'c'), (2, 'd'), (5, 'e'), (3, 'f'), (5, 'g'), (4, 'h'), (0, 'i')];

        MsvcSort.Sort<(int Key, char Name)>(items, ByKey);

        // Traced by hand: pivot e (the middle element), one partition, then
        // the selection sort of the first eight. The four 5s end g, a, c, e.
        Assert.Equal("ibdfhgace", new string(items.Select(i => i.Item2).ToArray()));
    }

    [Fact]
    public void SortsAndKeepsEveryElement()
    {
        var random = new GameRandom(42);
        for (var n = 0; n < 200; n++)
        {
            var items = Enumerable.Range(0, n).Select(i => (Key: random.Next(20), Name: (char)i)).ToArray();
            var before = items.OrderBy(i => i.Name).ToArray();

            MsvcSort.Sort<(int Key, char Name)>(items, ByKey);

            Assert.True(items.Zip(items.Skip(1)).All(p => p.First.Key <= p.Second.Key));
            Assert.Equal(before, items.OrderBy(i => i.Name).ToArray());
        }
    }
}
