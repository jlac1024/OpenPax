using System.Runtime.CompilerServices;
using OpenPax.Lua.Syntax;

namespace OpenPax.Lua.Runtime;

/// <summary>
/// A Lua world: its globals and the library, and calls into it from the
/// host. Each call from the host may run so many steps (statements and
/// calls); past that it stops with <see cref="LuaBudgetException"/>, the
/// same on every machine. Nothing here reaches the files, the clock or the
/// OS; the host gives what a script may use (docs/modding/lua.md).
/// </summary>
public sealed partial class LuaState
{
    /// <summary>The steps one call from the host may run, by default.</summary>
    public const long DefaultBudget = 10_000_000;

    // Script calls nested deeper than this are an error ("stack overflow"), well before the host's own stack runs out.
    private const int MaxDepth = 180;

    private readonly ConditionalWeakTable<object, object> _addresses = [];
    private long _nextAddress = 1;
    private long _remaining = DefaultBudget;
    private int _depth;

    public LuaState()
    {
        Library.BaseLibrary.Open(this);
        Library.StringLibrary.Open(this);
        Library.TableLibrary.Open(this);
        Library.MathLibrary.Open(this);
    }

    public LuaTable Globals { get; } = new();

    /// <summary>The strings' metatable: its __index is the string library, so s:upper() works.</summary>
    internal LuaTable StringMeta { get; } = new();

    /// <summary>The steps one call from the host may run.</summary>
    public long Budget { get; set; } = DefaultBudget;

    /// <summary>Where print's lines go (the mod's log); nowhere when null.</summary>
    public Action<string>? Output { get; set; }

    /// <summary>The random numbers math.random draws (the mod's own stream in a game); math.random is an error without one.</summary>
    public ILuaRandom? Random { get; set; }

    /// <summary>The chunk running, for error messages.</summary>
    internal string Chunk { get; set; } = "?";

    /// <summary>The line running, for error messages.</summary>
    internal int Line { get; set; }

    /// <summary>Compiles a script; a syntax error is a <see cref="LuaException"/> with its file and line.</summary>
    public LuaFunction Load(string source, string chunk)
    {
        var tokens = new Lexer(source, chunk).Read();
        var proto = new Compiler(tokens, chunk).CompileChunk();
        return new LuaClosure(proto, []);
    }

    /// <summary>
    /// Calls a function from the host with a fresh step budget. An error
    /// comes back as a <see cref="LuaException"/> with the file and line it
    /// came from; running out of budget as <see cref="LuaBudgetException"/>.
    /// </summary>
    public LuaValue[] Run(LuaFunction function, params LuaValue[] args)
    {
        var outermost = _depth == 0;
        if (outermost)
        {
            _remaining = Budget;
        }

        try
        {
            return Call(LuaValue.Of(function), args);
        }
        finally
        {
            if (outermost)
            {
                (Chunk, Line) = ("?", 0);
            }
        }
    }

    /// <summary>Compiles and runs a script, giving back what it returns.</summary>
    public LuaValue[] DoString(string source, string chunk = "script") => Run(Load(source, chunk));

    /// <summary>Calls a value: a function, or anything with __call.</summary>
    public LuaValue[] Call(LuaValue callee, LuaValue[] args)
    {
        if (callee.AsFunction is not { } function)
        {
            if (MetaOf(callee, "__call") is { IsNil: false } handler)
            {
                return Call(handler, [callee, .. args]);
            }

            throw Error($"attempt to call a {TypeName(callee)} value");
        }

        Spend();
        if (++_depth > MaxDepth || !RuntimeHelpers.TryEnsureSufficientExecutionStack())
        {
            _depth--;
            throw Error("stack overflow");
        }

        try
        {
            return function.Invoke(this, args);
        }
        catch (LuaException e) when (!e.Placed && function is LuaBuiltin)
        {
            // A library function's error is placed at the script line that called it.
            throw Placed(e.Value);
        }
        finally
        {
            _depth--;
        }
    }

    /// <summary>One step of the budget: each statement and call takes one.</summary>
    internal void Spend()
    {
        if (--_remaining < 0)
        {
            throw new LuaBudgetException($"{Chunk}:{Line}: the script ran too long (more than {Budget} steps)");
        }
    }

    /// <summary>An error at the running line.</summary>
    public LuaException Error(string message) => Placed(LuaValue.Of(message));

    internal LuaException Placed(LuaValue value) =>
        value.IsString ? new LuaException(LuaValue.Of($"{Chunk}:{Line}: {value.AsString}")) { Placed = true } : new LuaException(value) { Placed = true };

    /// <summary>A builtin function, ready to put in a table.</summary>
    public static LuaValue Function(string name, Func<LuaState, LuaValue[], LuaValue[]> body) => LuaValue.Of(new LuaBuiltin(name, body));

    // A stable made-up address for tostring(t): the order things were first printed, the same on every machine.
    internal string Address(object o)
    {
        var id = (long)_addresses.GetValue(o, _ => _nextAddress++);
        return "0x" + id.ToString("x8", System.Globalization.CultureInfo.InvariantCulture);
    }
}
