using System.Text;
using OpenPax.Lua.Runtime;

namespace OpenPax.Lua.Library;

/// <summary>Lua's table library. sort is a stable merge sort: the same order on every machine whatever the comparison.</summary>
internal static class TableLibrary
{
    public static void Open(LuaState state)
    {
        var lib = new LuaTable();
        state.Globals.Set("table", lib);
        Add(lib, "insert", Insert);
        Add(lib, "remove", Remove);
        Add(lib, "concat", (s, a) =>
        {
            var t = Args.Table(a, 0, "concat");
            var separator = Args.OptString(a, 1, "concat") ?? "";
            var from = Args.OptInteger(a, 2, "concat", 1);
            var to = Args.OptInteger(a, 3, "concat", t.Length);
            var text = new StringBuilder();
            for (var i = from; i <= to; i++)
            {
                var v = t.Get(i);
                if (!v.IsString && !v.IsNumber)
                {
                    throw new LuaException($"invalid value (at index {i}) in table for 'concat'");
                }

                text.Append(v.ToString());
                if (i < to)
                {
                    text.Append(separator);
                }
            }

            return Args.One(text.ToString());
        });
        Add(lib, "unpack", (_, a) =>
        {
            var t = Args.Table(a, 0, "unpack");
            var from = Args.OptInteger(a, 1, "unpack", 1);
            var to = Args.OptInteger(a, 2, "unpack", t.Length);
            if (to - from >= 100_000)
            {
                throw new LuaException("too many results to unpack");
            }

            var values = new List<LuaValue>();
            for (var i = from; i <= to; i++)
            {
                values.Add(t.Get(i));
            }

            return [.. values];
        });
        Add(lib, "pack", (_, a) =>
        {
            var t = new LuaTable();
            for (var i = 0; i < a.Length; i++)
            {
                t.Set(i + 1L, a[i]);
            }

            t.Set("n", (long)a.Length);
            return Args.One(t);
        });
        Add(lib, "sort", Sort);
    }

    private static void Add(LuaTable table, string name, Func<LuaState, LuaValue[], LuaValue[]> body) => table.Set(name, LuaState.Function(name, body));

    private static LuaValue[] Insert(LuaState state, LuaValue[] a)
    {
        var t = Args.Table(a, 0, "insert");
        var n = t.Length;
        if (a.Length == 2)
        {
            t.Set(n + 1, a[1]);
            return Args.None;
        }

        if (a.Length != 3)
        {
            throw new LuaException("wrong number of arguments to 'insert'");
        }

        var at = Args.Integer(a, 1, "insert");
        if (at < 1 || at > n + 1)
        {
            throw Args.Bad(1, "insert", "position out of bounds");
        }

        for (var i = n; i >= at; i--)
        {
            t.Set(i + 1, t.Get(i));
        }

        t.Set(at, a[2]);
        return Args.None;
    }

    private static LuaValue[] Remove(LuaState state, LuaValue[] a)
    {
        var t = Args.Table(a, 0, "remove");
        var n = t.Length;
        var at = Args.OptInteger(a, 1, "remove", n);
        // As Lua 5.4: any position from 1 to n + 1, or n itself (0 for an empty table).
        if (at != n && (ulong)(at - 1) > (ulong)n)
        {
            throw Args.Bad(1, "remove", "position out of bounds");
        }

        var removed = t.Get(at);
        for (; at < n; at++)
        {
            t.Set(at, t.Get(at + 1));
        }

        t.Set(at, LuaValue.Nil);
        return Args.One(removed);
    }

    private static LuaValue[] Sort(LuaState state, LuaValue[] a)
    {
        var t = Args.Table(a, 0, "sort");
        var compare = Args.At(a, 1);
        if (!compare.IsNil && compare.AsFunction is null)
        {
            throw Args.Bad(1, "sort", "function expected");
        }

        var n = (int)t.Length;
        var values = new LuaValue[n];
        for (var i = 0; i < n; i++)
        {
            values[i] = t.Get(i + 1L);
        }

        bool Less(LuaValue x, LuaValue y) => compare.IsNil ? state.LessThan(x, y) : LuaState.First(state.Call(compare, [x, y])).IsTruthy;
        var sorted = MergeSort(values, Less);
        for (var i = 0; i < n; i++)
        {
            t.Set(i + 1L, sorted[i]);
        }

        return Args.None;
    }

    private static LuaValue[] MergeSort(LuaValue[] values, Func<LuaValue, LuaValue, bool> less)
    {
        if (values.Length <= 1)
        {
            return values;
        }

        var middle = values.Length / 2;
        var left = MergeSort(values[..middle], less);
        var right = MergeSort(values[middle..], less);
        var merged = new LuaValue[values.Length];
        int i = 0, j = 0, k = 0;
        while (i < left.Length && j < right.Length)
        {
            // The right one goes first only when strictly less: equal ones keep their order.
            merged[k++] = less(right[j], left[i]) ? right[j++] : left[i++];
        }

        while (i < left.Length)
        {
            merged[k++] = left[i++];
        }

        while (j < right.Length)
        {
            merged[k++] = right[j++];
        }

        return merged;
    }
}
