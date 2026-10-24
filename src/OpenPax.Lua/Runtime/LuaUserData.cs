namespace OpenPax.Lua.Runtime;

/// <summary>
/// A host object shown to scripts (a player, a ship), read through its
/// metatable's __index. Two views of the same object are equal.
/// </summary>
public sealed class LuaUserData(object host, LuaTable metatable)
{
    public object Host { get; } = host;

    public LuaTable Metatable { get; } = metatable;
}
