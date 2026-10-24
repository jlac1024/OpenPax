namespace OpenPax.Lua.Syntax;

/// <summary>Where a name lives: a local slot of the function, one of its upvalues, or the globals.</summary>
internal enum VarKind
{
    Local,
    Upvalue,
    Global,
}
