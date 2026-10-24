using System.Text;
using OpenPax.Lua.Runtime;

namespace OpenPax.Lua.Library;

/// <summary>
/// Lua's string library, on the game's text: a string is its characters
/// (so # and the positions count characters). Strings share it as their
/// metatable's __index, so s:upper() works.
/// </summary>
internal static class StringLibrary
{
    public static void Open(LuaState state)
    {
        var lib = new LuaTable();
        state.Globals.Set("string", lib);
        state.StringMeta.Set("__index", lib);
        Add(lib, "len", (_, a) => Args.One((long)Args.String(a, 0, "len").Length));
        Add(lib, "upper", (_, a) => Args.One(Args.String(a, 0, "upper").ToUpperInvariant()));
        Add(lib, "lower", (_, a) => Args.One(Args.String(a, 0, "lower").ToLowerInvariant()));
        Add(lib, "reverse", (_, a) =>
        {
            var chars = Args.String(a, 0, "reverse").ToCharArray();
            Array.Reverse(chars);
            return Args.One(new string(chars));
        });
        Add(lib, "sub", (_, a) =>
        {
            var s = Args.String(a, 0, "sub");
            var (from, to) = Range(s.Length, Args.OptInteger(a, 1, "sub", 1), Args.OptInteger(a, 2, "sub", -1));
            return Args.One(from > to ? "" : s.Substring((int)from - 1, (int)(to - from + 1)));
        });
        Add(lib, "rep", (_, a) =>
        {
            var s = Args.String(a, 0, "rep");
            var n = Args.Integer(a, 1, "rep");
            var separator = Args.OptString(a, 2, "rep") ?? "";
            if (n <= 0)
            {
                return Args.One("");
            }

            if ((s.Length + separator.Length) * n > 10_000_000)
            {
                throw new LuaException("resulting string too large");
            }

            return Args.One(string.Join(separator, Enumerable.Repeat(s, (int)n)));
        });
        Add(lib, "byte", (_, a) =>
        {
            var s = Args.String(a, 0, "byte");
            var i = Args.OptInteger(a, 1, "byte", 1);
            var (from, to) = Range(s.Length, i, Args.OptInteger(a, 2, "byte", i));
            var values = new List<LuaValue>();
            for (var k = from; k <= to; k++)
            {
                values.Add(LuaValue.Of((long)s[(int)k - 1]));
            }

            return [.. values];
        });
        Add(lib, "char", (_, a) =>
        {
            var text = new StringBuilder();
            for (var i = 0; i < a.Length; i++)
            {
                var c = Args.Integer(a, i, "char");
                if (c is < 0 or > 0xFFFF)
                {
                    throw Args.Bad(i, "char", "value out of range");
                }

                text.Append((char)c);
            }

            return Args.One(text.ToString());
        });
        Add(lib, "format", (s, a) => Args.One(StringFormat.Format(s, a)));
        Add(lib, "find", (_, a) => Find(a, "find", true));
        Add(lib, "match", (_, a) => Find(a, "match", false));
        Add(lib, "gmatch", (_, a) =>
        {
            var text = Args.String(a, 0, "gmatch");
            var pattern = Args.String(a, 1, "gmatch");
            var all = LuaPattern.All(text, pattern).ToList();
            var next = 0;
            return Args.One(LuaState.Function("gmatch_iterator", (_, _) => next < all.Count ? all[next++].Captures : Args.One(LuaValue.Nil)));
        });
        Add(lib, "gsub", Gsub);
    }

    private static void Add(LuaTable table, string name, Func<LuaState, LuaValue[], LuaValue[]> body) => table.Set(name, LuaState.Function(name, body));

    // Lua's string positions: from 1, negative from the end, clipped to the string.
    private static (long From, long To) Range(int length, long from, long to)
    {
        if (from < 0)
        {
            from = Math.Max(length + from + 1, 1);
        }
        else if (from == 0)
        {
            from = 1;
        }

        if (to < 0)
        {
            to = length + to + 1;
        }
        else if (to > length)
        {
            to = length;
        }

        return (from, to);
    }

    private static LuaValue[] Find(LuaValue[] a, string name, bool find)
    {
        var s = Args.String(a, 0, name);
        var pattern = Args.String(a, 1, name);
        var init = Args.OptInteger(a, 2, name, 1);
        init = init < 0 ? Math.Max(s.Length + init + 1, 1) : init == 0 ? 1 : init;
        if (init > s.Length + 1)
        {
            return Args.One(LuaValue.Nil);
        }

        var start = (int)init - 1;
        var plain = find && Args.At(a, 3).IsTruthy;
        if (find && (plain || pattern.AsSpan().IndexOfAny("^$*+?.([%-") < 0))
        {
            var at = s.IndexOf(pattern, start, StringComparison.Ordinal);
            return at < 0 ? Args.One(LuaValue.Nil) : [LuaValue.Of(at + 1L), LuaValue.Of((long)(at + pattern.Length))];
        }

        // match gives the captures (the whole match when there are none); find gives the place, then any captures.
        if (LuaPattern.Find(s, pattern, start, wholeIfNone: !find) is not { } m)
        {
            return Args.One(LuaValue.Nil);
        }

        return find ? [LuaValue.Of(m.Start + 1L), LuaValue.Of((long)m.End), .. m.Captures] : m.Captures;
    }

    private static LuaValue[] Gsub(LuaState state, LuaValue[] a)
    {
        var s = Args.String(a, 0, "gsub");
        var pattern = Args.String(a, 1, "gsub");
        var replacement = Args.At(a, 2);
        if (!(replacement.IsString || replacement.IsNumber || replacement.Type is LuaType.Table or LuaType.Function))
        {
            throw Args.Bad(2, "gsub", $"string/function/table expected, got {LuaState.TypeName(replacement)}");
        }

        var limit = Args.OptInteger(a, 3, "gsub", long.MaxValue);
        var text = new StringBuilder();
        var at = 0;
        long count = 0;
        foreach (var (start, end, captures) in LuaPattern.All(s, pattern))
        {
            if (count >= limit)
            {
                break;
            }

            text.Append(s, at, start - at);
            var whole = s[start..end];
            var value = replacement.Type switch
            {
                LuaType.Table => state.Index(replacement, captures[0]),
                LuaType.Function => LuaState.First(state.Call(replacement, captures)),
                _ => LuaValue.Of(Expand(replacement.ToString(), whole, captures)),
            };

            // false or nil keeps the match as it was.
            text.Append(value.IsTruthy ? value.IsString || value.IsNumber ? value.ToString() : throw new LuaException($"invalid replacement value (a {LuaState.TypeName(value)})") : whole);
            at = end;
            count++;
        }

        text.Append(s, at, s.Length - at);
        return [LuaValue.Of(text.ToString()), LuaValue.Of(count)];
    }

    // %0 is the whole match, %1 to %9 the captures, %% a percent sign.
    private static string Expand(string replacement, string whole, LuaValue[] captures)
    {
        var text = new StringBuilder();
        for (var i = 0; i < replacement.Length; i++)
        {
            var c = replacement[i];
            if (c != '%')
            {
                text.Append(c);
                continue;
            }

            if (++i >= replacement.Length)
            {
                throw new LuaException("invalid use of '%' in replacement string");
            }

            var d = replacement[i];
            if (d == '%')
            {
                text.Append('%');
            }
            else if (d == '0')
            {
                text.Append(whole);
            }
            else if (char.IsAsciiDigit(d))
            {
                var index = d - '1';
                text.Append(index < captures.Length ? captures[index].ToString() : throw new LuaException($"invalid capture index %{d} in replacement string"));
            }
            else
            {
                throw new LuaException("invalid use of '%' in replacement string");
            }
        }

        return text.ToString();
    }
}
