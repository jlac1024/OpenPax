using OpenPax.Lua.Runtime;

namespace OpenPax.Lua.Syntax;

/// <summary>
/// A function being compiled: its locals in scope (each with a frame slot,
/// freed when its block ends) and the upvalues it takes from the functions
/// around it, which a closure collects when it is made.
/// </summary>
internal sealed class FunctionScope(FunctionScope? parent, FunctionProto proto)
{
    private readonly List<(string Name, int Slot)> _locals = [];
    private readonly List<int> _blocks = [];
    private readonly List<string> _upvalueNames = [];

    public FunctionProto Proto { get; } = proto;

    public int SlotCount { get; private set; }

    /// <summary>A new local, in the slot after the ones in scope.</summary>
    public int Declare(string name)
    {
        var slot = _locals.Count;
        _locals.Add((name, slot));
        SlotCount = Math.Max(SlotCount, _locals.Count);
        return slot;
    }

    public void OpenBlock() => _blocks.Add(_locals.Count);

    public void CloseBlock()
    {
        var start = _blocks[^1];
        _blocks.RemoveAt(_blocks.Count - 1);
        _locals.RemoveRange(start, _locals.Count - start);
    }

    /// <summary>What a name means here: the innermost local, else an upvalue from an enclosing function, else a global.</summary>
    public (VarKind Kind, int Index) Resolve(string name)
    {
        for (var i = _locals.Count - 1; i >= 0; i--)
        {
            if (_locals[i].Name == name)
            {
                return (VarKind.Local, _locals[i].Slot);
            }
        }

        var known = _upvalueNames.IndexOf(name);
        if (known >= 0)
        {
            return (VarKind.Upvalue, known);
        }

        if (parent is null)
        {
            return (VarKind.Global, 0);
        }

        var (kind, index) = parent.Resolve(name);
        if (kind == VarKind.Global)
        {
            return (VarKind.Global, 0);
        }

        Proto.Upvalues.Add((kind == VarKind.Local, index));
        _upvalueNames.Add(name);
        return (VarKind.Upvalue, _upvalueNames.Count - 1);
    }
}
