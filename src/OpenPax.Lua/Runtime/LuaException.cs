namespace OpenPax.Lua.Runtime;

/// <summary>A Lua error: what error() raised, or the interpreter's own, with the script's file and line where known.</summary>
public sealed class LuaException : Exception
{
    public LuaException()
        : this("error")
    {
    }

    public LuaException(string message)
        : this(LuaValue.Of(message))
    {
    }

    public LuaException(string message, Exception inner)
        : base(message, inner)
    {
        Value = LuaValue.Of(message);
    }

    public LuaException(LuaValue value)
        : base(value.IsString ? value.AsString : value.ToString())
    {
        Value = value;
    }

    /// <summary>The error's value, as pcall gives it back.</summary>
    public LuaValue Value { get; }

    /// <summary>Whether the file and line have been put in front of the message.</summary>
    internal bool Placed { get; init; }
}
