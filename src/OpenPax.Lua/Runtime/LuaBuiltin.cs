namespace OpenPax.Lua.Runtime;

/// <summary>A function the host gives scripts: the standard library and the game's API.</summary>
public sealed class LuaBuiltin(string name, Func<LuaState, LuaValue[], LuaValue[]> body) : LuaFunction
{
    public override string Name { get; } = name;

    internal override LuaValue[] Invoke(LuaState state, LuaValue[] args) => body(state, args);
}
