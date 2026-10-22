using OpenPax.Data.Resources;
using OpenPax.Data.Text;
using OpenPax.Sim.Galaxy;

namespace OpenPax.Sim.Tests;

public class UniverseGeneratorTests
{
    private static readonly NameTables _syntheticNames = new(
        [.. Enumerable.Range(0, 103).Select(i => $"Star{i}")],
        ["I", "II", "III", "IV", "V", "VI"],
        "Jump to ");

    [Fact]
    public void SameSeedSameGalaxyDifferentSeedDifferentGalaxy()
    {
        Assert.Equal(Hash(1234, 40), Hash(1234, 40));
        Assert.NotEqual(Hash(1234, 40), Hash(1235, 40));
        Assert.NotEqual(Hash(1234, 40), Hash(1234, 41));
    }

    [Theory]
    [InlineData(1u, 2)]
    [InlineData(77u, 25)]
    [InlineData(24999u, 100)]
    public void GalaxiesFollowTheOriginalsRules(uint seed, int stars)
    {
        var universe = Generate(seed, stars, out _);

        Assert.Equal(stars, universe.Stars.Count);
        foreach (var s in universe.Stars)
        {
            Assert.InRange(s.X, 20, 430);
            Assert.InRange(s.Y, 20, 430);
            Assert.InRange(s.Type, 0, 6);
            Assert.All(universe.Stars.Where(o => o.Index < s.Index), o => Assert.False(Math.Abs(o.X - s.X) < 30 && Math.Abs(o.Y - s.Y) < 30));

            Assert.InRange(s.Planets.Count, 2, 6);
            Assert.Equal(s.Planets.Select(p => p.Slot).Order(), s.Planets.Select(p => p.Slot));
            Assert.Equal(s.Planets.Count, s.Planets.Select(p => p.Slot).Distinct().Count());
            Assert.Contains(s.Planets, p => p.Type != 4);
            foreach (var p in s.Planets)
            {
                Assert.Equal((s.Type - p.Slot + 3) * 25, p.Temperature);
                Assert.True(p.Type == 4 ? p.Class == 5 && p.Temperature <= 0 : p.Class is >= 0 and <= 4);
                Assert.InRange(p.Moons, 0, p.Type - 1);
                Assert.Equal(18, p.Satellites.Count);
            }

            // Every wormhole end is linked both ways, to another star.
            Assert.All(s.Wormholes, w => Assert.True(w.Partner?.Partner == w && w.Partner.Star != s.Index));
        }

        // A spanning tree: 2(N - 1) ends, and every star reachable.
        Assert.Equal(2 * (stars - 1), universe.Stars.Sum(s => s.Wormholes.Count));
        var seen = new HashSet<int> { 0 };
        var queue = new Queue<int>([0]);
        while (queue.TryDequeue(out var i))
        {
            foreach (var w in universe.Stars[i].Wormholes)
            {
                // Linked ends were checked above.
                if (seen.Add(w.Partner!.Star))
                {
                    queue.Enqueue(w.Partner.Star);
                }
            }
        }

        Assert.Equal(stars, seen.Count);
    }

    [Theory]
    [InlineData(1)]
    [InlineData(101)]
    public void RefusesStarCountsTheOriginalCannotBuild(int stars)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => Generate(1, stars, out _));
    }

    [Fact]
    public void ObjectIdsGoStarsThenPlanetsThenWormholes()
    {
        var universe = Generate(5, 10, out var ids);

        Assert.Equal(Enumerable.Range(0, 10), universe.Stars.Select(s => s.ObjectId));
        var planetIds = universe.Stars.SelectMany(s => s.Planets).Select(p => p.ObjectId).ToArray();
        var wormholeIds = universe.Stars.SelectMany(s => s.Wormholes).Select(w => w.ObjectId).ToArray();
        Assert.True(planetIds.All(i => i >= 10 && i < 10 + planetIds.Length));
        Assert.Equal(Enumerable.Range(10 + planetIds.Length, wormholeIds.Length), wormholeIds);
        Assert.Equal(10 + planetIds.Length + wormholeIds.Length, ids.Next);
    }

    [Fact]
    public void RealNameListsGiveAStableGalaxy()
    {
        var file = ResourceFile.Load(Path.Combine(GameData.RequireFolder(), "Data", "StrData"));
        IReadOnlyList<string> List(int id) => StringList.Parse(file.Get(ResourceType.StringList, id).Span);
        var names = new NameTables(List(1000), List(111), List(110)[2]);

        var universe = UniverseGenerator.Generate(1234, 30, names, new GameRandom(0), new ObjectIds());

        Assert.StartsWith("Jump to ", universe.Stars[0].Wormholes[0].Name);
        Assert.EndsWith(" " + List(111)[universe.Stars[0].Planets[0].Slot], universe.Stars[0].Planets[0].Name);
        var hash = new StateHash();
        universe.AddTo(hash);
        // Pinned so any change to the order of draws shows up. It records
        // the port's current result; the original's galaxy for this seed
        // has not been captured.
        Assert.Equal(16163521632646025612UL, hash.Value);
    }

    private static ulong Hash(uint seed, int stars)
    {
        var hash = new StateHash();
        Generate(seed, stars, out _).AddTo(hash);
        return hash.Value;
    }

    private static Universe Generate(uint seed, int stars, out ObjectIds ids)
    {
        ids = new ObjectIds();
        return UniverseGenerator.Generate(seed, stars, _syntheticNames, new GameRandom(0), ids);
    }
}
