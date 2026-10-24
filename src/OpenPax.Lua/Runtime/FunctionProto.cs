namespace OpenPax.Lua.Runtime;

/// <summary>
/// A function as compiled: its parameters, how many local slots it needs,
/// where its upvalues come from in the enclosing function, and its body.
/// </summary>
internal sealed class FunctionProto(string name, string chunk, int line)
{
    public string Name { get; } = name;

    public string Chunk { get; } = chunk;

    public int Line { get; } = line;

    public int Parameters { get; set; }

    public bool IsVararg { get; set; }

    public int Slots { get; set; }

    /// <summary>For each upvalue: a local slot of the enclosing function's frame, or one of its upvalues.</summary>
    public List<(bool FromLocal, int Index)> Upvalues { get; } = [];

    public Stat Body { get; set; } = _ => Signal.Normal;
}
