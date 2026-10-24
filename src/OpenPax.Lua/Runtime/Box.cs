namespace OpenPax.Lua.Runtime;

/// <summary>A local variable's cell: closures that use the variable share it, so each sees the others' changes.</summary>
internal sealed class Box(LuaValue value)
{
    public LuaValue Value = value;
}
