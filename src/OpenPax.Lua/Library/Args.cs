using OpenPax.Lua.Runtime;

namespace OpenPax.Lua.Library;

/// <summary>A library function's arguments checked, with Lua's wording when one is wrong.</summary>
internal static class Args
{
    public static LuaValue At(LuaValue[] args, int i) => i < args.Length ? args[i] : LuaValue.Nil;

    public static LuaException Bad(int i, string function, string message) => new($"bad argument #{i + 1} to '{function}' ({message})");

    private static LuaException Expected(LuaValue[] args, int i, string function, string what) =>
        Bad(i, function, $"{what} expected, got {(i < args.Length ? LuaState.TypeName(args[i]) : "no value")}");

    public static LuaValue Any(LuaValue[] args, int i, string function) =>
        i < args.Length ? args[i] : throw Bad(i, function, "value expected");

    public static LuaTable Table(LuaValue[] args, int i, string function) =>
        At(args, i).AsTable ?? throw Expected(args, i, function, "table");

    public static LuaFunction Function(LuaValue[] args, int i, string function) =>
        At(args, i).AsFunction ?? throw Expected(args, i, function, "function");

    /// <summary>A string, or a number written as one, as Lua's library takes them.</summary>
    public static string String(LuaValue[] args, int i, string function)
    {
        var v = At(args, i);
        return v.IsString ? v.AsString : v.IsNumber ? v.ToString() : throw Expected(args, i, function, "string");
    }

    public static string? OptString(LuaValue[] args, int i, string function) => At(args, i).IsNil ? null : String(args, i, function);

    public static LuaValue Number(LuaValue[] args, int i, string function)
    {
        var n = Numbers.Coerce(At(args, i));
        return n.IsNumber ? n : throw Expected(args, i, function, "number");
    }

    public static long Integer(LuaValue[] args, int i, string function)
    {
        var v = At(args, i);
        if (Numbers.ToInteger(v) is { } n)
        {
            return n;
        }

        return Numbers.Coerce(v).IsNumber ? throw Bad(i, function, "number has no integer representation") : throw Expected(args, i, function, "number");
    }

    public static long OptInteger(LuaValue[] args, int i, string function, long fallback) => At(args, i).IsNil ? fallback : Integer(args, i, function);

    public static LuaValue[] One(LuaValue v) => [v];

    public static LuaValue[] None => [];
}
