using OpenPax.Lua.Runtime;

namespace OpenPax.Lua.Library;

/// <summary>
/// Lua's math library, as far as it comes out the same on every machine:
/// no sin, cos, tan, exp or log for now (the C library's results differ
/// between systems), random from the host's deterministic stream.
/// </summary>
internal static class MathLibrary
{
    public static void Open(LuaState state)
    {
        var lib = new LuaTable();
        state.Globals.Set("math", lib);
        lib.Set("pi", Math.PI);
        lib.Set("huge", double.PositiveInfinity);
        lib.Set("maxinteger", long.MaxValue);
        lib.Set("mininteger", long.MinValue);
        Add(lib, "abs", (_, a) =>
        {
            var n = Args.Number(a, 0, "abs");
            return Args.One(n.IsInteger ? LuaValue.Of(unchecked(n.AsInteger < 0 ? -n.AsInteger : n.AsInteger)) : LuaValue.Of(Math.Abs(n.AsFloat)));
        });
        Add(lib, "floor", (_, a) => Args.One(Round(Args.Number(a, 0, "floor"), Math.Floor)));
        Add(lib, "ceil", (_, a) => Args.One(Round(Args.Number(a, 0, "ceil"), Math.Ceiling)));
        Add(lib, "sqrt", (_, a) => Args.One(Math.Sqrt(Args.Number(a, 0, "sqrt").AsFloat)));
        Add(lib, "fmod", (s, a) =>
        {
            var (x, y) = (Args.Number(a, 0, "fmod"), Args.Number(a, 1, "fmod"));
            if (x.IsInteger && y.IsInteger)
            {
                if (y.AsInteger == 0)
                {
                    throw Args.Bad(1, "fmod", "zero");
                }

                return Args.One(y.AsInteger == -1 ? 0L : x.AsInteger % y.AsInteger);
            }

            return Args.One(x.AsFloat % y.AsFloat);
        });
        Add(lib, "modf", (_, a) =>
        {
            var d = Args.Number(a, 0, "modf").AsFloat;
            var whole = d >= 0 ? Math.Floor(d) : Math.Ceiling(d);
            var wholeValue = Numbers.FloatToInteger(whole) is { } i && double.IsFinite(d) ? LuaValue.Of((double)i) : LuaValue.Of(whole);
            return [wholeValue, LuaValue.Of(double.IsInfinity(d) ? 0.0 : d - whole)];
        });
        Add(lib, "tointeger", (_, a) =>
        {
            var v = Args.At(a, 0);
            return Args.One(v.IsInteger ? v : v.IsFloat && Numbers.FloatToInteger(v.AsFloat) is { } i ? LuaValue.Of(i) : LuaValue.Nil);
        });
        Add(lib, "type", (_, a) =>
        {
            var v = Args.Any(a, 0, "type");
            return Args.One(v.IsInteger ? LuaValue.Of("integer") : v.IsFloat ? LuaValue.Of("float") : LuaValue.Nil);
        });
        Add(lib, "ult", (_, a) => Args.One((ulong)Args.Integer(a, 0, "ult") < (ulong)Args.Integer(a, 1, "ult")));
        Add(lib, "max", (s, a) => Args.One(Extreme(s, a, "max", (x, y) => s.LessThan(x, y))));
        Add(lib, "min", (s, a) => Args.One(Extreme(s, a, "min", (x, y) => s.LessThan(y, x))));
        Add(lib, "random", Random);
        Add(lib, "randomseed", (_, _) => Args.None);
    }

    private static void Add(LuaTable table, string name, Func<LuaState, LuaValue[], LuaValue[]> body) => table.Set(name, LuaState.Function(name, body));

    // floor and ceil give an integer when it fits, as in Lua 5.4.
    private static LuaValue Round(LuaValue n, Func<double, double> round)
    {
        if (n.IsInteger)
        {
            return n;
        }

        var d = round(n.AsFloat);
        return Numbers.FloatToInteger(d) is { } i ? LuaValue.Of(i) : LuaValue.Of(d);
    }

    private static LuaValue Extreme(LuaState state, LuaValue[] a, string name, Func<LuaValue, LuaValue, bool> better)
    {
        var best = Args.Number(a, 0, name);
        for (var i = 1; i < a.Length; i++)
        {
            var v = Args.Number(a, i, name);
            if (better(best, v))
            {
                best = v;
            }
        }

        return best;
    }

    private static LuaValue[] Random(LuaState state, LuaValue[] a)
    {
        var random = state.Random ?? throw new LuaException("math.random is only available while a game runs (docs/modding/lua.md)");
        if (a.Length == 0)
        {
            return Args.One(random.NextDouble());
        }

        var (low, high) = a.Length == 1 ? (1L, Args.Integer(a, 0, "random")) : (Args.Integer(a, 0, "random"), Args.Integer(a, 1, "random"));
        if (low > high)
        {
            throw Args.Bad(a.Length == 1 ? 0 : 1, "random", "interval is empty");
        }

        var span = (ulong)(high - low) + 1;
        if (span == 0 || span > long.MaxValue)
        {
            throw Args.Bad(0, "random", "interval too large");
        }

        return Args.One(low + random.Next((long)span));
    }
}
