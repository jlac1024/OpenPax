namespace OpenPax.Lua.Runtime;

/// <summary>
/// A script ran past its step budget or too deep in calls. pcall does not
/// catch it: it ends the call from the game, the same on every machine.
/// </summary>
public sealed class LuaBudgetException : Exception
{
    public LuaBudgetException()
        : this("the script ran too long")
    {
    }

    public LuaBudgetException(string message)
        : base(message)
    {
    }

    public LuaBudgetException(string message, Exception inner)
        : base(message, inner)
    {
    }
}
