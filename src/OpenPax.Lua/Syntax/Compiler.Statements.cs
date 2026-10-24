using OpenPax.Lua.Runtime;

namespace OpenPax.Lua.Syntax;

/// <summary>The statements.</summary>
internal sealed partial class Compiler
{
    /// <summary>A place a value can go: a variable, or a key of a table.</summary>
    private sealed record Target(VarKind Kind, int Index, string Name, Expr? Table = null, Expr? Key = null);

    private Stat? Statement()
    {
        var line = Current.Line;
        Enter();
        try
        {
            switch (Current.Kind)
            {
                case TokenKind.Semicolon:
                    Advance();
                    return null;
                case TokenKind.If:
                    return Placed(line, If());
                case TokenKind.While:
                    return Placed(line, While());
                case TokenKind.Do:
                    {
                        Advance();
                        var body = Block();
                        Expect(TokenKind.EndKeyword, "end");
                        return body;
                    }

                case TokenKind.For:
                    return Placed(line, For());
                case TokenKind.Repeat:
                    return Placed(line, Repeat());
                case TokenKind.Function:
                    return Placed(line, FunctionStatement());
                case TokenKind.Local:
                    Advance();
                    return Placed(line, Accept(TokenKind.Function) ? LocalFunction() : Local());
                case TokenKind.Break:
                    Advance();
                    if (_loops == 0)
                    {
                        throw Error("break outside a loop", line);
                    }

                    return _ => Signal.Break;
                case TokenKind.Goto or TokenKind.DoubleColon:
                    throw Error("goto and labels are not supported (docs/modding/lua.md)", line);
                default:
                    return Placed(line, ExpressionStatement());
            }
        }
        finally
        {
            Leave();
        }
    }

    private Stat Return()
    {
        Advance();
        var values = BlockEnds() || Check(TokenKind.Semicolon) ? [] : ExpressionList();
        Accept(TokenKind.Semicolon);
        if (!BlockEnds())
        {
            throw Error($"'end' expected near {Near(Current)}");
        }

        var all = AllValues(values);
        return f =>
        {
            f.Returned = all(f);
            return Signal.Return;
        };
    }

    private Stat If()
    {
        var branches = new List<(Expr Condition, Stat Body)>();
        do
        {
            Advance();
            var condition = Expression().Single;
            Expect(TokenKind.Then, "then");
            branches.Add((condition, Block()));
        }
        while (Check(TokenKind.ElseIf));

        var otherwise = Accept(TokenKind.Else) ? Block() : null;
        Expect(TokenKind.EndKeyword, "end");
        var all = branches.ToArray();
        return f =>
        {
            foreach (var (condition, body) in all)
            {
                if (condition(f).IsTruthy)
                {
                    return body(f);
                }
            }

            return otherwise?.Invoke(f) ?? Signal.Normal;
        };
    }

    private Stat While()
    {
        Advance();
        var condition = Expression().Single;
        Expect(TokenKind.Do, "do");
        var body = LoopBody(Block);
        Expect(TokenKind.EndKeyword, "end");
        return f =>
        {
            while (condition(f).IsTruthy)
            {
                f.State.Spend();
                var signal = body(f);
                if (signal == Signal.Break)
                {
                    break;
                }

                if (signal == Signal.Return)
                {
                    return signal;
                }
            }

            return Signal.Normal;
        };
    }

    private Stat Repeat()
    {
        Advance();
        // The condition sees the body's locals, so the block closes after it.
        _function.OpenBlock();
        var body = LoopBody(Statements);
        Expect(TokenKind.Until, "until");
        var condition = Expression().Single;
        _function.CloseBlock();
        return f =>
        {
            while (true)
            {
                f.State.Spend();
                var signal = body(f);
                if (signal == Signal.Break)
                {
                    break;
                }

                if (signal == Signal.Return)
                {
                    return signal;
                }

                if (condition(f).IsTruthy)
                {
                    break;
                }
            }

            return Signal.Normal;
        };
    }

    private Stat LoopBody(Func<Stat> body)
    {
        _loops++;
        try
        {
            return body();
        }
        finally
        {
            _loops--;
        }
    }

    private Stat For()
    {
        Advance();
        var first = ExpectName();
        return Check(TokenKind.Assign) ? NumericFor(first) : GenericFor(first);
    }

    private Stat NumericFor(string name)
    {
        Advance();
        var start = Expression().Single;
        Expect(TokenKind.Comma, ",");
        var limit = Expression().Single;
        var step = Accept(TokenKind.Comma) ? Expression().Single : null;
        Expect(TokenKind.Do, "do");
        _function.OpenBlock();
        var slot = _function.Declare(name);
        var body = LoopBody(Block);
        _function.CloseBlock();
        Expect(TokenKind.EndKeyword, "end");
        return f => ForLoop.Numeric(f, start(f), limit(f), step is null ? LuaValue.Of(1L) : step(f), slot, body);
    }

    private Stat GenericFor(string first)
    {
        var names = new List<string> { first };
        while (Accept(TokenKind.Comma))
        {
            names.Add(ExpectName());
        }

        Expect(TokenKind.In, "in");
        var iterator = Adjusted(ExpressionList(), 3);
        Expect(TokenKind.Do, "do");
        _function.OpenBlock();
        var slots = names.Select(_function.Declare).ToArray();
        var body = LoopBody(Block);
        _function.CloseBlock();
        Expect(TokenKind.EndKeyword, "end");
        return f => ForLoop.Generic(f, iterator(f), slots, body);
    }

    private Stat FunctionStatement()
    {
        var line = Advance().Line;
        var name = ExpectName();
        var (kind, index) = _function.Resolve(name);
        var target = new Target(kind, index, name);
        var fullName = name;
        var method = false;
        while (Check(TokenKind.Dot) || Check(TokenKind.Colon))
        {
            method = Advance().Kind == TokenKind.Colon;
            var key = ExpectName();
            var table = Read(target);
            target = new Target(VarKind.Global, 0, key, table, _ => LuaValue.Of(key));
            fullName += (method ? ":" : ".") + key;
            if (method)
            {
                break;
            }
        }

        var function = FunctionBody(fullName, method, line).Single;
        var assign = Assigner(target);
        return f =>
        {
            assign(f, function(f));
            return Signal.Normal;
        };
    }

    private Stat LocalFunction()
    {
        var line = Current.Line;
        var name = ExpectName();
        // Declared before its body, so the function can call itself.
        var slot = _function.Declare(name);
        var function = FunctionBody(name, false, line).Single;
        return f =>
        {
            var box = new Box(LuaValue.Nil);
            f.Locals[slot] = box;
            box.Value = function(f);
            return Signal.Normal;
        };
    }

    private Stat Local()
    {
        var names = new List<string>();
        do
        {
            names.Add(ExpectName());
            if (Accept(TokenKind.Less))
            {
                var attribute = ExpectName();
                if (attribute != "const")
                {
                    throw Error($"unknown or unsupported attribute '{attribute}'");
                }

                Expect(TokenKind.Greater, ">");
            }
        }
        while (Accept(TokenKind.Comma));

        // The values are compiled before the names come into scope: local x = x reads the outer x.
        var values = Accept(TokenKind.Assign) ? ExpressionList() : [];
        var slots = names.Select(_function.Declare).ToArray();
        if (slots.Length == 1 && values.Count == 1)
        {
            var (slot, value) = (slots[0], values[0].Single);
            return f =>
            {
                f.Locals[slot] = new Box(value(f));
                return Signal.Normal;
            };
        }

        var all = Adjusted(values, slots.Length);
        return f =>
        {
            var v = all(f);
            for (var i = 0; i < slots.Length; i++)
            {
                f.Locals[slots[i]] = new Box(v[i]);
            }

            return Signal.Normal;
        };
    }

    private Stat ExpressionStatement()
    {
        var (value, target) = Suffixed();
        if (Check(TokenKind.Assign) || Check(TokenKind.Comma))
        {
            if (target is null)
            {
                throw Error("syntax error near " + Near(Current));
            }

            return Assignment(target);
        }

        if (value.Multi is not { } call)
        {
            throw Error("syntax error near " + Near(Current));
        }

        return f =>
        {
            call(f);
            return Signal.Normal;
        };
    }

    private Stat Assignment(Target first)
    {
        var targets = new List<Target> { first };
        while (Accept(TokenKind.Comma))
        {
            var (_, target) = Suffixed();
            targets.Add(target ?? throw Error("syntax error near " + Near(Current)));
        }

        Expect(TokenKind.Assign, "=");
        var values = ExpressionList();
        if (targets.Count == 1 && values.Count == 1)
        {
            var (assign, value) = (Assigner(targets[0]), values[0].Single);
            return f =>
            {
                assign(f, value(f));
                return Signal.Normal;
            };
        }

        // Tables and keys first, then the values, then the stores left to right.
        var places = targets.Select(Place).ToArray();
        var all = Adjusted(values, targets.Count);
        return f =>
        {
            var stores = new Action<LuaValue>[places.Length];
            for (var i = 0; i < places.Length; i++)
            {
                stores[i] = places[i](f);
            }

            var v = all(f);
            for (var i = 0; i < stores.Length; i++)
            {
                stores[i](v[i]);
            }

            return Signal.Normal;
        };
    }

    /// <summary>Stores a value at a target, working out its table and key at the time.</summary>
    private static Action<Frame, LuaValue> Assigner(Target target) => target.Table is { } table
        ? (f, v) => f.State.SetIndex(table(f), target.Key!(f), v) // A table target always has its key.
        : target.Kind switch
        {
            VarKind.Local => (f, v) => f.Locals[target.Index].Value = v,
            VarKind.Upvalue => (f, v) => f.Up[target.Index].Value = v,
            _ => (f, v) => f.State.Globals.Set(target.Name, v),
        };

    /// <summary>A target's table and key worked out now, and a store for its value later.</summary>
    private static Func<Frame, Action<LuaValue>> Place(Target target)
    {
        if (target.Table is { } table)
        {
            var key = target.Key!; // A table target always has its key.
            return f =>
            {
                var (t, k) = (table(f), key(f));
                return v => f.State.SetIndex(t, k, v);
            };
        }

        var assign = Assigner(target);
        return f => v => assign(f, v);
    }

    /// <summary>Reads a target's value.</summary>
    private static Expr Read(Target target) => target.Table is { } table
        ? f => f.State.Index(table(f), target.Key!(f)) // A table target always has its key.
        : Variable(target.Kind, target.Index, target.Name);

    private static Expr Variable(VarKind kind, int index, string name) => kind switch
    {
        VarKind.Local => f => f.Locals[index].Value,
        VarKind.Upvalue => f => f.Up[index].Value,
        _ => f => f.State.Globals.Get(name),
    };
}
