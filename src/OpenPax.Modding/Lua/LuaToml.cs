using OpenPax.Lua.Runtime;

namespace OpenPax.Modding.Lua;

/// <summary>A TOML file's values as Lua's: tables for tables, lists from 1, and the plain values as they are.</summary>
internal static class LuaToml
{
    public static LuaValue ToLua(object? value) => value switch
    {
        null => LuaValue.Nil,
        string s => LuaValue.Of(s),
        long l => LuaValue.Of(l),
        double d => LuaValue.Of(d),
        bool b => LuaValue.Of(b),
        IReadOnlyDictionary<string, object> table => Table(table),
        System.Collections.IEnumerable list => List(list),
        _ => LuaValue.Of(value.ToString()),
    };

    private static LuaValue Table(IReadOnlyDictionary<string, object> table)
    {
        var t = new LuaTable();
        // Keys in a fixed order, so pairs over a settings file goes the same way everywhere.
        foreach (var (key, value) in table.OrderBy(kv => kv.Key, StringComparer.Ordinal))
        {
            t.Set(key, ToLua(value));
        }

        return LuaValue.Of(t);
    }

    private static LuaValue List(System.Collections.IEnumerable list)
    {
        var t = new LuaTable();
        long i = 1;
        foreach (var item in list)
        {
            t.Set(i++, ToLua(item));
        }

        return LuaValue.Of(t);
    }
}
