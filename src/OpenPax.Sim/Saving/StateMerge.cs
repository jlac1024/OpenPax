using System.Collections;
using System.Reflection;
using OpenPax.Sim.Combat;
using OpenPax.Sim.Galaxy;
using OpenPax.Sim.Players;
using OpenPax.Sim.Setup;
using OpenPax.Sim.Time;

namespace OpenPax.Sim.Saving;

/// <summary>
/// Brings a game up to a newer copy of itself in place, for a network
/// client: the client loads each state the server sends as a second game,
/// then this copies it into the game the screens hold. Objects with an
/// identity (players, stars, planets, wormholes, fleets, colonies, ships
/// and slot designs, advisors, missions, battles and what fights in them)
/// keep their instances, so what a screen holds stays live; everything else
/// is taken from the newer copy. The game tables are shared and never
/// walked. Afterwards the game hashes as the newer copy does.
/// </summary>
public static class StateMerge
{
    private const BindingFlags Instance = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly;

    public static void Into(Game game, Game newer)
    {
        var tables = new SaveTables(game.Tables);
        var olds = Keyed(game, tables);
        var news = Keyed(newer, tables);

        // A key found twice on either side is left alone rather than guessed at.
        var target = new Dictionary<object, object>(ReferenceEqualityComparer.Instance) { [newer] = game };
        foreach (var (key, found) in news)
        {
            if (found.Count == 1 && olds.TryGetValue(key, out var old) && old.Count == 1)
            {
                target[found[0]] = old[0];
            }
        }

        var all = new List<object>();
        Walk(newer, tables, all.Add);
        object? Map(object? value) => value is not null && target.TryGetValue(value, out var t) ? t : value;
        foreach (var item in all)
        {
            switch (item)
            {
                case Array array when !array.GetType().GetElementType()!.IsPrimitive:
                    for (var i = 0; i < array.Length; i++)
                    {
                        array.SetValue(Remap(array.GetValue(i), Map), i);
                    }

                    break;
                case IList list when !IsLeafCollection(item.GetType()):
                    for (var i = 0; i < list.Count; i++)
                    {
                        list[i] = Remap(list[i], Map);
                    }

                    break;
                case IDictionary dictionary when !IsLeafCollection(item.GetType()):
                    var entries = dictionary.Cast<DictionaryEntry>().Select(e => (Key: Remap(e.Key, Map)!, Value: Remap(e.Value, Map))).ToList();
                    dictionary.Clear();
                    foreach (var (k, v) in entries)
                    {
                        dictionary[k] = v;
                    }

                    break;
                case IEnumerable when IsLeafCollection(item.GetType()):
                    break;
                default:
                    var into = Map(item)!;
                    foreach (var field in Fields(item.GetType()))
                    {
                        field.SetValue(into, Remap(field.GetValue(item), Map));
                    }

                    break;
            }
        }
    }

    /// <summary>The identity of an object a screen may hold on to, or null for one that is taken as new each time.</summary>
    private static long? Key(object value) => value switch
    {
        Game or Universe or Calendar => (long?)0,
        Player p => p.Index,
        Star s => s.Index,
        StarCenter c => c.Star,
        Planet p => p.ObjectId,
        Wormhole w => w.ObjectId,
        Fleet f => f.ObjectId,
        Colony c => c.Planet.ObjectId,
        Design { ShipId: >= 0 } d => d.ShipId,
        Design { Slot: >= 0, Owner: { } owner } d => -1 - (owner.Index * 1000L) - d.Slot,
        Advisor a => a.Id,
        SpyMission m => m.Id,
        Battle b => b.Id,
        BattleObject o => o.Id,
        _ => null,
    };

    private static Dictionary<(Type, long), List<object>> Keyed(Game game, SaveTables tables)
    {
        var keyed = new Dictionary<(Type, long), List<object>>();
        Walk(game, tables, o =>
        {
            if (Key(o) is { } key)
            {
                var k = (o.GetType(), key);
                if (!keyed.TryGetValue(k, out var list))
                {
                    keyed[k] = list = [];
                }

                list.Add(o);
            }
        });
        return keyed;
    }

    /// <summary>Every object reachable from the root once, the game tables and their contents aside.</summary>
    private static void Walk(object root, SaveTables tables, Action<object> visit)
    {
        var seen = new HashSet<object>(ReferenceEqualityComparer.Instance);
        var stack = new Stack<object>();
        stack.Push(root);
        while (stack.Count > 0)
        {
            var item = stack.Pop();
            if (!seen.Add(item))
            {
                continue;
            }

            visit(item);
            foreach (var child in Children(item))
            {
                if (child is not null && !IsLeaf(child, tables))
                {
                    stack.Push(child);
                }
            }
        }
    }

    private static IEnumerable<object?> Children(object item)
    {
        switch (item)
        {
            case Array array:
                if (!array.GetType().GetElementType()!.IsPrimitive)
                {
                    foreach (var e in array)
                    {
                        foreach (var r in References(e))
                        {
                            yield return r;
                        }
                    }
                }

                yield break;
            case IDictionary dictionary:
                foreach (DictionaryEntry e in dictionary)
                {
                    yield return e.Key;
                    yield return e.Value;
                }

                yield break;
            case IEnumerable items when item.GetType().IsGenericType && item is not string:
                foreach (var e in items)
                {
                    foreach (var r in References(e))
                    {
                        yield return r;
                    }
                }

                yield break;
        }

        foreach (var field in Fields(item.GetType()))
        {
            foreach (var r in References(field.GetValue(item)))
            {
                yield return r;
            }
        }
    }

    /// <summary>The objects a value refers to: itself, or for a struct the objects its fields refer to.</summary>
    private static IEnumerable<object> References(object? value)
    {
        if (value is null || value is string || value.GetType().IsPrimitive || value is Enum)
        {
            yield break;
        }

        if (!value.GetType().IsValueType)
        {
            yield return value;
            yield break;
        }

        foreach (var field in Fields(value.GetType()))
        {
            foreach (var r in References(field.GetValue(value)))
            {
                yield return r;
            }
        }
    }

    /// <summary>A value with its objects mapped; a struct is copied with its fields mapped.</summary>
    private static object? Remap(object? value, Func<object?, object?> map)
    {
        if (value is null || value is string || value.GetType().IsPrimitive || value is Enum)
        {
            return value;
        }

        if (!value.GetType().IsValueType)
        {
            return map(value);
        }

        var fields = Fields(value.GetType()).ToList();
        if (fields.Count == 0)
        {
            return value;
        }

        foreach (var field in fields)
        {
            field.SetValue(value, Remap(field.GetValue(value), map));
        }

        return value;
    }

    private static bool IsLeaf(object value, SaveTables tables) =>
        value is string or Type or Delegate or MemberInfo || tables.TryPlace(value, out _) || value is GameTables || value is IComputerPlayer;

    /// <summary>Sets and dictionaries of plain values, taken whole.</summary>
    private static bool IsLeafCollection(Type type) =>
        type.IsGenericType && type.GetGenericArguments().All(a => a.IsPrimitive || a.IsEnum || a == typeof(string))
        && type.GetGenericTypeDefinition() != typeof(List<>);

    private static IEnumerable<FieldInfo> Fields(Type type)
    {
        for (var t = type; t is not null && t != typeof(object); t = t.BaseType)
        {
            foreach (var f in t.GetFields(Instance))
            {
                if (!f.IsLiteral && !f.IsStatic && !f.IsDefined(typeof(NotSavedAttribute)))
                {
                    yield return f;
                }
            }
        }
    }
}
