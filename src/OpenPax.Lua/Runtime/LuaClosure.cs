namespace OpenPax.Lua.Runtime;

/// <summary>A script's function with the variables it closed over.</summary>
internal sealed class LuaClosure(FunctionProto proto, Box[] upvalues) : LuaFunction
{
    public override string Name => proto.Name;

    internal FunctionProto Proto => proto;

    internal override LuaValue[] Invoke(LuaState state, LuaValue[] args)
    {
        var varargs = proto.IsVararg && args.Length > proto.Parameters ? args[proto.Parameters..] : [];
        var frame = new Frame(state, upvalues, proto.Slots, varargs);
        for (var i = 0; i < proto.Parameters; i++)
        {
            frame.Locals[i] = new Box(i < args.Length ? args[i] : LuaValue.Nil);
        }

        var (chunk, line) = (state.Chunk, state.Line);
        state.Chunk = proto.Chunk;
        try
        {
            return proto.Body(frame) == Signal.Return ? frame.Returned : [];
        }
        finally
        {
            (state.Chunk, state.Line) = (chunk, line);
        }
    }
}
