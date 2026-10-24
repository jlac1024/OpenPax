namespace OpenPax.Lua.Runtime;

/// <summary>One call of a script function: its locals, its upvalues, its extra arguments and what it returns.</summary>
internal sealed class Frame(LuaState state, Box[] upvalues, int slots, LuaValue[] varargs)
{
    public LuaState State { get; } = state;

    public Box[] Up { get; } = upvalues;

    public Box[] Locals { get; } = new Box[slots];

    public LuaValue[] Varargs { get; } = varargs;

    public LuaValue[] Returned { get; set; } = [];
}
