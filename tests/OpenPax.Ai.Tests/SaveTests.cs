using System.Collections;
using System.Reflection;
using OpenPax.Sim;
using OpenPax.Sim.Saving;
using OpenPax.Sim.Setup;

namespace OpenPax.Ai.Tests;

public class SaveTests
{
    private static Game RoundTrip(Game game)
    {
        using var stream = new MemoryStream();
        SaveFile.Write(stream, game, game.Tables);
        stream.Position = 0;
        Assert.True(SaveFile.IsSave(stream));
        stream.Position = 0;
        return Assert.IsType<Game>(SaveFile.Read(stream, game.Tables, game.ComputerPlayers));
    }

    [Fact]
    public void ALoadedGameIsTheSameGameFieldForField()
    {
        var game = AiGames.New(seed: 4242, players: 6);
        AiGames.Run(game, 3000);
        var loaded = RoundTrip(game);

        Assert.Equal(game.Hash(), loaded.Hash());
        Assert.Same(game.ComputerPlayers, loaded.ComputerPlayers);
        Assert.Same(game.Tables, loaded.Tables);
        Same(game, loaded, game.Tables, "game", new HashSet<(object, object)>(PairComparer.Instance));
    }

    [Fact]
    public void ALoadedGameGoesOnTickForTick()
    {
        var game = AiGames.New(seed: 99, players: 5);
        AiGames.Run(game, 2500);
        var loaded = RoundTrip(game);
        for (var i = 0; i < 2000; i++)
        {
            game.Step();
            loaded.Step();
            Assert.Equal(game.Hash(), loaded.Hash());
        }
    }

    [Fact]
    public void SharedObjectsStayShared()
    {
        var game = AiGames.New(seed: 7, players: 4);
        AiGames.Run(game, 1500);
        var loaded = RoundTrip(game);
        foreach (var player in loaded.Players)
        {
            foreach (var fleet in player.Fleets)
            {
                Assert.Contains(fleet, loaded.Universe.Stars[fleet.Star].Fleets);
                Assert.All(fleet.Ships, s => Assert.Same(fleet, s.Fleet));
            }

            foreach (var colony in player.Colonies)
            {
                Assert.Same(colony, colony.Planet.Colony);
                Assert.Same(player, colony.Owner);
            }
        }
    }

    [Fact]
    public void OtherFilesAreRefused()
    {
        using var stream = new MemoryStream([1, 2, 3, 4, 5, 6, 7, 8]);
        Assert.False(SaveFile.IsSave(stream));
        stream.Position = 0;
        Assert.Throws<InvalidDataException>(() => SaveFile.Read(stream, AiGames.Tables(), null));
    }

    /// <summary>Walks both graphs together; game data and the computer player module must be the very same objects.</summary>
    private static void Same(object? a, object? b, GameTables tables, string path, HashSet<(object, object)> seen)
    {
        if (a is null || b is null)
        {
            Assert.True(a is null && b is null, path);
            return;
        }

        var type = a.GetType();
        Assert.Equal(type, b.GetType());
        if (type.IsPrimitive || a is string || a is Enum || a is decimal)
        {
            Assert.True(a.Equals(b), $"{path}: {a} != {b}");
            return;
        }

        if (a is GameTables || a is IComputerPlayer || IsTableObject(a, tables))
        {
            Assert.Same(a, b);
            return;
        }

        if (!type.IsValueType && !seen.Add((a, b)))
        {
            return;
        }

        if (a is IEnumerable ea && b is IEnumerable eb && (a is Array || a is ICollection))
        {
            var la = ea.Cast<object?>().ToList();
            var lb = eb.Cast<object?>().ToList();
            Assert.True(la.Count == lb.Count, path + " count");
            for (var i = 0; i < la.Count; i++)
            {
                Same(la[i], lb[i], tables, $"{path}[{i}]", seen);
            }

            return;
        }

        for (var t = type; t is not null && t != typeof(object) && t != typeof(ValueType); t = t.BaseType)
        {
            foreach (var f in t.GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly))
            {
                Same(f.GetValue(a), f.GetValue(b), tables, path + "." + f.Name, seen);
            }
        }
    }

    private static bool IsTableObject(object o, GameTables tables)
    {
        return ReferenceEquals(o, tables.GalaxyNames) || tables.Species.Any(s => ReferenceEquals(s, o))
            || tables.Techs.Any(t => ReferenceEquals(t, o)) || tables.Hulls.Any(h => ReferenceEquals(h, o));
    }

    private sealed class PairComparer : IEqualityComparer<(object, object)>
    {
        public static readonly PairComparer Instance = new();

        public bool Equals((object, object) x, (object, object) y) => ReferenceEquals(x.Item1, y.Item1) && ReferenceEquals(x.Item2, y.Item2);

        public int GetHashCode((object, object) obj) => HashCode.Combine(
            System.Runtime.CompilerServices.RuntimeHelpers.GetHashCode(obj.Item1),
            System.Runtime.CompilerServices.RuntimeHelpers.GetHashCode(obj.Item2));
    }
}
