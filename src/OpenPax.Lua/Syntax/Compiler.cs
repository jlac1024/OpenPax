using OpenPax.Lua.Runtime;

namespace OpenPax.Lua.Syntax;

/// <summary>
/// Lua source to runnable code in one pass: a recursive descent parser
/// that gives back C# closures (statements, expressions) instead of a
/// syntax tree. Each local gets a frame slot; a closure takes the boxes
/// of the variables it uses when it is made.
/// </summary>
internal sealed partial class Compiler(List<Token> tokens, string chunk)
{
    // Nesting deeper than this (blocks, parentheses) is refused, before the compiler's own stack runs low.
    private const int MaxNesting = 200;

    private int _at;
    private int _nesting;
    private int _loops;
    private FunctionScope _function = null!; // Set by CompileChunk before anything reads it.

    /// <summary>A compiled expression: one value, and for a call or ... all of them.</summary>
    private sealed record ExprInfo(Expr Single, MultiExpr? Multi = null, string? Description = null);

    public FunctionProto CompileChunk()
    {
        var proto = new FunctionProto("main chunk", chunk, 0) { IsVararg = true };
        _function = new FunctionScope(null, proto);
        proto.Body = Block();
        if (Current.Kind != TokenKind.End)
        {
            throw Error($"'<eof>' expected near {Near(Current)}");
        }

        proto.Slots = _function.SlotCount;
        return proto;
    }

    private Token Current => tokens[_at];

    private Token Advance() => tokens[_at++];

    private bool Check(TokenKind kind) => Current.Kind == kind;

    private bool Accept(TokenKind kind)
    {
        if (Current.Kind != kind)
        {
            return false;
        }

        _at++;
        return true;
    }

    private Token Expect(TokenKind kind, string what)
    {
        if (Current.Kind != kind)
        {
            throw Error($"'{what}' expected near {Near(Current)}");
        }

        return Advance();
    }

    private string ExpectName() => Expect(TokenKind.Name, "<name>").Text;

    private static string Near(Token t) => t.Kind switch
    {
        TokenKind.End => "<eof>",
        TokenKind.String => $"'{t.Text}'",
        TokenKind.Name or TokenKind.Integer or TokenKind.Float => $"'{t.Text}'",
        _ => $"'{Symbol(t.Kind)}'",
    };

    private static string Symbol(TokenKind kind) => kind switch
    {
        TokenKind.Plus => "+",
        TokenKind.Minus => "-",
        TokenKind.Star => "*",
        TokenKind.Slash => "/",
        TokenKind.DoubleSlash => "//",
        TokenKind.Percent => "%",
        TokenKind.Caret => "^",
        TokenKind.Hash => "#",
        TokenKind.Ampersand => "&",
        TokenKind.Tilde => "~",
        TokenKind.Pipe => "|",
        TokenKind.ShiftLeft => "<<",
        TokenKind.ShiftRight => ">>",
        TokenKind.Equal => "==",
        TokenKind.NotEqual => "~=",
        TokenKind.LessEqual => "<=",
        TokenKind.GreaterEqual => ">=",
        TokenKind.Less => "<",
        TokenKind.Greater => ">",
        TokenKind.Assign => "=",
        TokenKind.OpenParen => "(",
        TokenKind.CloseParen => ")",
        TokenKind.OpenBrace => "{",
        TokenKind.CloseBrace => "}",
        TokenKind.OpenBracket => "[",
        TokenKind.CloseBracket => "]",
        TokenKind.DoubleColon => "::",
        TokenKind.Semicolon => ";",
        TokenKind.Colon => ":",
        TokenKind.Comma => ",",
        TokenKind.Dot => ".",
        TokenKind.Concat => "..",
        TokenKind.Ellipsis => "...",
        _ => kind.ToString().ToLowerInvariant().Replace("keyword", "", StringComparison.Ordinal),
    };

    private LuaException Error(string message, int? line = null) => new($"{chunk}:{line ?? Current.Line}: {message}") { Placed = true };

    private void Enter()
    {
        if (++_nesting > MaxNesting)
        {
            throw Error("chunk has too many syntax levels");
        }
    }

    private void Leave() => _nesting--;

    private bool BlockEnds() => Current.Kind is TokenKind.End or TokenKind.EndKeyword or TokenKind.Else or TokenKind.ElseIf or TokenKind.Until;

    /// <summary>Statements to the end of the block, in a scope of their own.</summary>
    private Stat Block()
    {
        _function.OpenBlock();
        var block = Statements();
        _function.CloseBlock();
        return block;
    }

    private Stat Statements()
    {
        var list = new List<Stat>();
        while (!BlockEnds())
        {
            if (Check(TokenKind.Return))
            {
                list.Add(Placed(Current.Line, Return()));
                break;
            }

            if (Statement() is { } s)
            {
                list.Add(s);
            }
        }

        return Sequence(list);
    }

    private static Stat Sequence(List<Stat> list)
    {
        if (list.Count == 0)
        {
            return _ => Signal.Normal;
        }

        if (list.Count == 1)
        {
            return list[0];
        }

        var all = list.ToArray();
        return f =>
        {
            foreach (var s in all)
            {
                var signal = s(f);
                if (signal != Signal.Normal)
                {
                    return signal;
                }
            }

            return Signal.Normal;
        };
    }

    // Each statement marks its line for errors and takes one step of the budget.
    private static Stat Placed(int line, Stat inner) => f =>
    {
        var state = f.State;
        state.Line = line;
        state.Spend();
        return inner(f);
    };

    private static ExprInfo Single(Expr e, string? description = null) => new(e, null, description);

    private static ExprInfo Multi(MultiExpr m) => new(f => LuaState.First(m(f)), m);

    /// <summary>A list of expressions as all their values: each gives one, but the last gives all of its own.</summary>
    private static MultiExpr AllValues(List<ExprInfo> list)
    {
        if (list.Count == 0)
        {
            return _ => [];
        }

        if (list[^1].Multi is not { } tail)
        {
            var singles = list.Select(e => e.Single).ToArray();
            return singles.Length == 1 ? f => [singles[0](f)] : f =>
            {
                var values = new LuaValue[singles.Length];
                for (var i = 0; i < singles.Length; i++)
                {
                    values[i] = singles[i](f);
                }

                return values;
            };
        }

        if (list.Count == 1)
        {
            return tail;
        }

        var head = list.Take(list.Count - 1).Select(e => e.Single).ToArray();
        return f =>
        {
            var first = new LuaValue[head.Length];
            for (var i = 0; i < head.Length; i++)
            {
                first[i] = head[i](f);
            }

            var rest = tail(f);
            return [.. first, .. rest];
        };
    }

    /// <summary>A list of expressions as exactly n values, cut or filled with nil.</summary>
    private static MultiExpr Adjusted(List<ExprInfo> list, int n)
    {
        var all = AllValues(list);
        return f =>
        {
            var values = all(f);
            if (values.Length == n)
            {
                return values;
            }

            var fitted = new LuaValue[n];
            Array.Copy(values, fitted, Math.Min(n, values.Length));
            return fitted;
        };
    }
}
