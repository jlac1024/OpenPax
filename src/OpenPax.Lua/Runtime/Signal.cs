namespace OpenPax.Lua.Runtime;

/// <summary>How a statement ended: on to the next, out of the loop, or out of the function.</summary>
internal enum Signal
{
    Normal,
    Break,
    Return,
}
