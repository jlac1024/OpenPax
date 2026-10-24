using OpenPax.Lua.Runtime;

namespace OpenPax.Lua.Library;

/// <summary>
/// Lua's basic functions, without those that reach outside the script's
/// world (load, dofile, collectgarbage): print goes to the host's log.
/// </summary>
internal static class BaseLibrary
{
    public static void Open(LuaState state)
    {
        var g = state.Globals;
        g.Set("_G", g);
        g.Set("_VERSION", "Lua 5.4");
        Add(g, "print", (s, a) =>
        {
            s.Output?.Invoke(string.Join('\t', a.Select(s.ToDisplay)));
            return Args.None;
        });
        Add(g, "type", (_, a) => Args.One(LuaState.TypeName(Args.Any(a, 0, "type"))));
        Add(g, "tostring", (s, a) => Args.One(s.ToDisplay(Args.Any(a, 0, "tostring"))));
        Add(g, "tonumber", ToNumber);
        Add(g, "rawget", (_, a) => Args.One(Args.Table(a, 0, "rawget").Get(Args.At(a, 1))));
        Add(g, "rawset", (_, a) =>
        {
            Args.Table(a, 0, "rawset").Set(Args.At(a, 1), Args.At(a, 2));
            return Args.One(a[0]);
        });
        Add(g, "rawequal", (_, a) => Args.One(Args.At(a, 0).Equals(Args.At(a, 1))));
        Add(g, "rawlen", (_, a) => Args.At(a, 0) is { IsString: true } str ? Args.One((long)str.AsString.Length) : Args.One(Args.Table(a, 0, "rawlen").Length));
        Add(g, "next", (_, a) =>
        {
            var (key, value) = Args.Table(a, 0, "next").Next(Args.At(a, 1));
            return key.IsNil ? Args.One(LuaValue.Nil) : [key, value];
        });
        var next = g.Get("next");
        Add(g, "pairs", (s, a) =>
        {
            var t = Args.Any(a, 0, "pairs");
            if (s.MetaOf(t, "__pairs") is { IsNil: false } handler)
            {
                var r = s.Call(handler, [t]);
                return [Args.At(r, 0), Args.At(r, 1), Args.At(r, 2)];
            }

            Args.Table(a, 0, "pairs");
            return [next, t, LuaValue.Nil];
        });
        var ipairsStep = LuaState.Function("ipairs_iterator", (s, a) =>
        {
            var i = Args.Integer(a, 1, "ipairs") + 1;
            var v = s.Index(a[0], LuaValue.Of(i));
            return v.IsNil ? Args.One(LuaValue.Nil) : [LuaValue.Of(i), v];
        });
        Add(g, "ipairs", (_, a) => [ipairsStep, Args.Any(a, 0, "ipairs"), LuaValue.Of(0L)]);
        Add(g, "select", Select);
        Add(g, "setmetatable", (_, a) =>
        {
            var t = Args.Table(a, 0, "setmetatable");
            var m = Args.At(a, 1);
            if (!m.IsNil && m.AsTable is null)
            {
                throw Args.Bad(1, "setmetatable", "nil or table expected");
            }

            if (t.Metatable?.Get("__metatable") is { IsNil: false })
            {
                throw new LuaException("cannot change a protected metatable");
            }

            t.Metatable = m.AsTable;
            return Args.One(t);
        });
        Add(g, "getmetatable", (s, a) =>
        {
            var m = s.MetatableOf(Args.At(a, 0));
            if (m is null)
            {
                return Args.One(LuaValue.Nil);
            }

            var guarded = m.Get("__metatable");
            return Args.One(guarded.IsNil ? LuaValue.Of(m) : guarded);
        });
        Add(g, "assert", (_, a) =>
        {
            if (Args.Any(a, 0, "assert").IsTruthy)
            {
                return a;
            }

            throw a.Length > 1 ? new LuaException(a[1]) : new LuaException("assertion failed!");
        });
        Add(g, "error", (s, a) =>
        {
            var value = Args.At(a, 0);
            var level = Args.OptInteger(a, 1, "error", 1);
            // A message is placed at the line that raised it, unless level 0 asks for none.
            if (value.IsString && level > 0)
            {
                throw s.Error(value.AsString);
            }

            throw new LuaException(value) { Placed = true };
        });
        Add(g, "pcall", (s, a) =>
        {
            var f = Args.Any(a, 0, "pcall");
            try
            {
                return [LuaValue.True, .. s.Call(f, a[1..])];
            }
            catch (LuaException e)
            {
                return [LuaValue.False, e.Value];
            }
        });
        Add(g, "xpcall", (s, a) =>
        {
            var f = Args.Any(a, 0, "xpcall");
            var handler = Args.Any(a, 1, "xpcall");
            try
            {
                return [LuaValue.True, .. s.Call(f, a[2..])];
            }
            catch (LuaException e)
            {
                return [LuaValue.False, .. s.Call(handler, [e.Value])];
            }
        });
    }

    private static void Add(LuaTable table, string name, Func<LuaState, LuaValue[], LuaValue[]> body) => table.Set(name, LuaState.Function(name, body));

    private static LuaValue[] ToNumber(LuaState state, LuaValue[] a)
    {
        var v = Args.Any(a, 0, "tonumber");
        if (Args.At(a, 1).IsNil)
        {
            return Args.One(v.IsNumber ? v : v.IsString ? Numbers.Parse(v.AsString) : LuaValue.Nil);
        }

        var radix = Args.Integer(a, 1, "tonumber");
        if (radix is < 2 or > 36)
        {
            throw Args.Bad(1, "tonumber", "base out of range");
        }

        var text = Args.String(a, 0, "tonumber").Trim().ToLowerInvariant();
        var negative = text.StartsWith('-');
        if (negative)
        {
            text = text[1..];
        }

        if (text.Length == 0)
        {
            return Args.One(LuaValue.Nil);
        }

        long n = 0;
        foreach (var c in text)
        {
            var digit = char.IsAsciiDigit(c) ? c - '0' : char.IsAsciiLetterLower(c) ? c - 'a' + 10 : 99;
            if (digit >= radix)
            {
                return Args.One(LuaValue.Nil);
            }

            n = unchecked((n * radix) + digit);
        }

        return Args.One(negative ? unchecked(-n) : n);
    }

    private static LuaValue[] Select(LuaState state, LuaValue[] a)
    {
        var which = Args.At(a, 0);
        if (which.IsString && which.AsString == "#")
        {
            return Args.One((long)(a.Length - 1));
        }

        var n = Args.Integer(a, 0, "select");
        if (n < 0)
        {
            n = a.Length - 1 + n + 1;
            if (n < 1)
            {
                throw Args.Bad(0, "select", "index out of range");
            }
        }
        else if (n == 0)
        {
            throw Args.Bad(0, "select", "index out of range");
        }

        return n >= a.Length ? [] : a[(int)n..];
    }
}
