namespace OpenPax.Lua.Runtime;

/// <summary>A Lua function: a script's own (a closure) or the host's (a builtin).</summary>
public abstract class LuaFunction
{
    /// <summary>The name errors give it.</summary>
    public abstract string Name { get; }

    /// <summary>Calls it with these arguments; what it returns, any number of values.</summary>
    internal abstract LuaValue[] Invoke(LuaState state, LuaValue[] args);
}
