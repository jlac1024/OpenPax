using OpenPax.Lua.Runtime;

namespace OpenPax.Lua.Syntax;

/// <summary>The expressions.</summary>
internal sealed partial class Compiler
{
    // Unary operators bind tighter than every binary one but ^.
    private const int UnaryPriority = 12;

    private List<ExprInfo> ExpressionList()
    {
        var list = new List<ExprInfo> { Expression() };
        while (Accept(TokenKind.Comma))
        {
            list.Add(Expression());
        }

        return list;
    }

    // Lua's priorities, left and right: right lower than left makes the operator right associative (.., ^).
    private static (int Left, int Right)? Priority(TokenKind kind) => kind switch
    {
        TokenKind.Or => (1, 1),
        TokenKind.And => (2, 2),
        TokenKind.Less or TokenKind.Greater or TokenKind.LessEqual or TokenKind.GreaterEqual or TokenKind.NotEqual or TokenKind.Equal => (3, 3),
        TokenKind.Pipe => (4, 4),
        TokenKind.Tilde => (5, 5),
        TokenKind.Ampersand => (6, 6),
        TokenKind.ShiftLeft or TokenKind.ShiftRight => (7, 7),
        TokenKind.Concat => (9, 8),
        TokenKind.Plus or TokenKind.Minus => (10, 10),
        TokenKind.Star or TokenKind.Slash or TokenKind.DoubleSlash or TokenKind.Percent => (11, 11),
        TokenKind.Caret => (14, 13),
        _ => null,
    };

    private ExprInfo Expression(int limit = 0)
    {
        Enter();
        try
        {
            ExprInfo left;
            var line = Current.Line;
            if (Current.Kind is TokenKind.Not or TokenKind.Minus or TokenKind.Hash or TokenKind.Tilde)
            {
                var op = Advance().Kind;
                left = Unary(op, Expression(UnaryPriority).Single, line);
            }
            else
            {
                left = Simple();
            }

            while (Priority(Current.Kind) is { } p && p.Left > limit)
            {
                var opLine = Current.Line;
                var op = Advance().Kind;
                var right = Expression(p.Right);
                left = Single(Binary(op, left.Single, right.Single, opLine));
            }

            return left;
        }
        finally
        {
            Leave();
        }
    }

    private static ExprInfo Unary(TokenKind op, Expr operand, int line) => Single(op switch
    {
        TokenKind.Not => f => LuaValue.Of(!operand(f).IsTruthy),
        TokenKind.Minus => f =>
        {
            var v = operand(f);
            return v.IsInteger ? LuaValue.Of(unchecked(-v.AsInteger)) : v.IsFloat ? LuaValue.Of(-v.AsFloat) : At(f, line).Negate(v);
        }
        ,
        TokenKind.Hash => f => At(f, line).Length(operand(f)),
        _ => f => At(f, line).BitNot(operand(f)),
    });

    // An operator's errors are placed at its own line, which a long expression may not share with its statement.
    private static LuaState At(Frame f, int line)
    {
        f.State.Line = line;
        return f.State;
    }

    private static Expr Binary(TokenKind op, Expr l, Expr r, int line)
    {
        switch (op)
        {
            case TokenKind.And:
                return f => l(f) is var x && x.IsTruthy ? r(f) : x;
            case TokenKind.Or:
                return f => l(f) is var x && x.IsTruthy ? x : r(f);
            case TokenKind.Equal:
                return f => LuaValue.Of(f.State.AreEqual(l(f), r(f)));
            case TokenKind.NotEqual:
                return f => LuaValue.Of(!f.State.AreEqual(l(f), r(f)));
            case TokenKind.Less:
                return f =>
                {
                    var (x, y) = (l(f), r(f));
                    return LuaValue.Of(x.IsInteger && y.IsInteger ? x.AsInteger < y.AsInteger : At(f, line).LessThan(x, y));
                };
            case TokenKind.LessEqual:
                return f =>
                {
                    var (x, y) = (l(f), r(f));
                    return LuaValue.Of(x.IsInteger && y.IsInteger ? x.AsInteger <= y.AsInteger : At(f, line).LessOrEqual(x, y));
                };
            case TokenKind.Greater:
                return f =>
                {
                    var (x, y) = (l(f), r(f));
                    return LuaValue.Of(x.IsInteger && y.IsInteger ? x.AsInteger > y.AsInteger : At(f, line).LessThan(y, x));
                };
            case TokenKind.GreaterEqual:
                return f =>
                {
                    var (x, y) = (l(f), r(f));
                    return LuaValue.Of(x.IsInteger && y.IsInteger ? x.AsInteger >= y.AsInteger : At(f, line).LessOrEqual(y, x));
                };
            case TokenKind.Concat:
                return f =>
                {
                    var (x, y) = (l(f), r(f));
                    return x.IsString && y.IsString ? LuaValue.Of(x.AsString + y.AsString) : At(f, line).Concat(x, y);
                };
            case TokenKind.Plus:
                return f =>
                {
                    var (x, y) = (l(f), r(f));
                    return x.IsInteger && y.IsInteger ? LuaValue.Of(unchecked(x.AsInteger + y.AsInteger))
                        : x.IsFloat && y.IsFloat ? LuaValue.Of(x.AsFloat + y.AsFloat)
                        : At(f, line).Arithmetic(LuaState.Arith.Add, x, y);
                };
            case TokenKind.Minus:
                return f =>
                {
                    var (x, y) = (l(f), r(f));
                    return x.IsInteger && y.IsInteger ? LuaValue.Of(unchecked(x.AsInteger - y.AsInteger))
                        : x.IsFloat && y.IsFloat ? LuaValue.Of(x.AsFloat - y.AsFloat)
                        : At(f, line).Arithmetic(LuaState.Arith.Sub, x, y);
                };
            case TokenKind.Star:
                return f =>
                {
                    var (x, y) = (l(f), r(f));
                    return x.IsInteger && y.IsInteger ? LuaValue.Of(unchecked(x.AsInteger * y.AsInteger))
                        : x.IsFloat && y.IsFloat ? LuaValue.Of(x.AsFloat * y.AsFloat)
                        : At(f, line).Arithmetic(LuaState.Arith.Mul, x, y);
                };
        }

        var arith = op switch
        {
            TokenKind.Slash => LuaState.Arith.Div,
            TokenKind.DoubleSlash => LuaState.Arith.IDiv,
            TokenKind.Percent => LuaState.Arith.Mod,
            TokenKind.Caret => LuaState.Arith.Pow,
            TokenKind.Ampersand => LuaState.Arith.BAnd,
            TokenKind.Pipe => LuaState.Arith.BOr,
            TokenKind.Tilde => LuaState.Arith.BXor,
            TokenKind.ShiftLeft => LuaState.Arith.Shl,
            _ => LuaState.Arith.Shr,
        };
        return f => At(f, line).Arithmetic(arith, l(f), r(f));
    }

    private ExprInfo Simple()
    {
        var t = Current;
        switch (t.Kind)
        {
            case TokenKind.Integer:
                {
                    Advance();
                    var v = LuaValue.Of(t.Integer);
                    return Single(_ => v);
                }

            case TokenKind.Float:
                {
                    Advance();
                    var v = LuaValue.Of(t.Float);
                    return Single(_ => v);
                }

            case TokenKind.String:
                {
                    Advance();
                    var v = LuaValue.Of(t.Text);
                    return Single(_ => v);
                }

            case TokenKind.Nil:
                Advance();
                return Single(_ => LuaValue.Nil);
            case TokenKind.True:
                Advance();
                return Single(_ => LuaValue.True);
            case TokenKind.False:
                Advance();
                return Single(_ => LuaValue.False);
            case TokenKind.Ellipsis:
                Advance();
                if (!_function.Proto.IsVararg)
                {
                    throw Error("cannot use '...' outside a vararg function", t.Line);
                }

                return Multi(f => f.Varargs);
            case TokenKind.OpenBrace:
                return Single(TableConstructor());
            case TokenKind.Function:
                Advance();
                return FunctionBody("anonymous", false, t.Line);
            default:
                return Suffixed().Value;
        }
    }

    /// <summary>A name or parenthesized expression with its suffixes (.x, [k], calls); a target when it can be assigned to.</summary>
    private (ExprInfo Value, Target? Target) Suffixed()
    {
        ExprInfo value;
        Target? target = null;
        var line = Current.Line;
        if (Check(TokenKind.Name))
        {
            var name = Advance().Text;
            var (kind, index) = _function.Resolve(name);
            target = new Target(kind, index, name);
            var description = $"{(kind == VarKind.Local ? "local" : kind == VarKind.Upvalue ? "upvalue" : "global")} '{name}'";
            value = Single(Variable(kind, index, name), description);
        }
        else if (Accept(TokenKind.OpenParen))
        {
            // Parentheses keep only the first value of a call.
            value = Single(Expression().Single);
            Expect(TokenKind.CloseParen, ")");
        }
        else
        {
            throw Error("unexpected symbol near " + Near(Current));
        }

        while (true)
        {
            line = Current.Line;
            switch (Current.Kind)
            {
                case TokenKind.Dot:
                    {
                        Advance();
                        var key = ExpectName();
                        var (table, k) = (value.Single, LuaValue.Of(key));
                        target = new Target(VarKind.Global, 0, key, table, _ => k);
                        value = Single(f => At(f, line).Index(table(f), k), $"field '{key}'");
                        break;
                    }

                case TokenKind.OpenBracket:
                    {
                        Advance();
                        var (table, key) = (value.Single, Expression().Single);
                        Expect(TokenKind.CloseBracket, "]");
                        target = new Target(VarKind.Global, 0, "?", table, key);
                        value = Single(f => At(f, line).Index(table(f), key(f)));
                        break;
                    }

                case TokenKind.Colon:
                    {
                        Advance();
                        var name = ExpectName();
                        var (self, key) = (value.Single, LuaValue.Of(name));
                        var args = Arguments();
                        target = null;
                        value = Multi(f =>
                        {
                            var o = self(f);
                            var method = At(f, line).Index(o, key);
                            var given = args(f);
                            return Call(f, line, method, [o, .. given], $"method '{name}'");
                        });
                        break;
                    }

                case TokenKind.OpenParen or TokenKind.String or TokenKind.OpenBrace:
                    {
                        var (callee, description) = (value.Single, value.Description);
                        var args = Arguments();
                        target = null;
                        value = Multi(f =>
                        {
                            var function = callee(f);
                            return Call(f, line, function, args(f), description);
                        });
                        break;
                    }

                default:
                    return (value, target);
            }
        }
    }

    private static LuaValue[] Call(Frame f, int line, LuaValue function, LuaValue[] args, string? description)
    {
        var state = At(f, line);
        if (function.AsFunction is null && state.MetaOf(function, "__call").IsNil)
        {
            throw state.Error($"attempt to call a {LuaState.TypeName(function)} value{(description is null ? "" : $" ({description})")}");
        }

        var values = state.Call(function, args);
        state.Line = line;
        return values;
    }

    private MultiExpr Arguments()
    {
        if (Check(TokenKind.String))
        {
            var v = LuaValue.Of(Advance().Text);
            return _ => [v];
        }

        if (Check(TokenKind.OpenBrace))
        {
            var table = TableConstructor();
            return f => [table(f)];
        }

        Expect(TokenKind.OpenParen, "(");
        var args = Check(TokenKind.CloseParen) ? [] : ExpressionList();
        Expect(TokenKind.CloseParen, ")");
        return AllValues(args);
    }

    private Expr TableConstructor()
    {
        Expect(TokenKind.OpenBrace, "{");
        var keyed = new List<(Expr? Key, ExprInfo Value)>();
        while (!Check(TokenKind.CloseBrace))
        {
            if (Accept(TokenKind.OpenBracket))
            {
                var key = Expression().Single;
                Expect(TokenKind.CloseBracket, "]");
                Expect(TokenKind.Assign, "=");
                keyed.Add((key, Expression()));
            }
            else if (Check(TokenKind.Name) && tokens[_at + 1].Kind == TokenKind.Assign)
            {
                var name = LuaValue.Of(Advance().Text);
                Advance();
                keyed.Add((_ => name, Expression()));
            }
            else
            {
                keyed.Add((null, Expression()));
            }

            if (!Accept(TokenKind.Comma) && !Accept(TokenKind.Semicolon))
            {
                break;
            }
        }

        Expect(TokenKind.CloseBrace, "}");
        var fields = keyed.ToArray();
        // The last field without a key gives all its values (a call, ...).
        var lastPositional = Array.FindLastIndex(fields, x => x.Key is null);
        return f =>
        {
            var table = new LuaTable();
            long n = 1;
            for (var i = 0; i < fields.Length; i++)
            {
                var (key, value) = fields[i];
                if (key is not null)
                {
                    f.State.SetIndex(LuaValue.Of(table), key(f), value.Single(f));
                }
                else if (i == lastPositional && i == fields.Length - 1 && value.Multi is { } all)
                {
                    foreach (var v in all(f))
                    {
                        table.Set(n++, v);
                    }
                }
                else
                {
                    table.Set(n++, value.Single(f));
                }
            }

            return LuaValue.Of(table);
        };
    }

    private ExprInfo FunctionBody(string name, bool method, int line)
    {
        var proto = new FunctionProto(name, chunk, line);
        var outer = _function;
        var outerLoops = _loops;
        _function = new FunctionScope(outer, proto);
        _loops = 0;
        if (method)
        {
            _function.Declare("self");
        }

        Expect(TokenKind.OpenParen, "(");
        if (!Check(TokenKind.CloseParen))
        {
            do
            {
                if (Accept(TokenKind.Ellipsis))
                {
                    proto.IsVararg = true;
                    break;
                }

                _function.Declare(ExpectName());
            }
            while (Accept(TokenKind.Comma));
        }

        Expect(TokenKind.CloseParen, ")");
        proto.Parameters = _function.SlotCount;
        proto.Body = Block();
        Expect(TokenKind.EndKeyword, "end");
        proto.Slots = _function.SlotCount;
        _function = outer;
        _loops = outerLoops;

        var upvalues = proto.Upvalues;
        return Single(f =>
        {
            var boxes = new Box[upvalues.Count];
            for (var i = 0; i < boxes.Length; i++)
            {
                var (fromLocal, index) = upvalues[i];
                boxes[i] = fromLocal ? f.Locals[index] : f.Up[index];
            }

            return LuaValue.Of(new LuaClosure(proto, boxes));
        });
    }
}
