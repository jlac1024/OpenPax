namespace OpenPax.Lua.Runtime;

/// <summary>The operators and indexing, with metatables, as Lua 5.4 does them.</summary>
public sealed partial class LuaState
{
    /// <summary>The operators that fall back on a metamethod, and its name.</summary>
    internal enum Arith
    {
        Add,
        Sub,
        Mul,
        Div,
        Mod,
        Pow,
        IDiv,
        BAnd,
        BOr,
        BXor,
        Shl,
        Shr,
    }

    private static readonly string[] _arithEvents = ["__add", "__sub", "__mul", "__div", "__mod", "__pow", "__idiv", "__band", "__bor", "__bxor", "__shl", "__shr"];

    public static string TypeName(LuaValue v) => v.Type switch
    {
        LuaType.Nil => "nil",
        LuaType.Boolean => "boolean",
        LuaType.Number => "number",
        LuaType.String => "string",
        LuaType.Table => "table",
        LuaType.Function => "function",
        _ => "userdata",
    };

    public LuaTable? MetatableOf(LuaValue v) => v.Type switch
    {
        LuaType.Table => v.AsTable!.Metatable, // A table by its type.
        LuaType.UserData => v.AsUserData!.Metatable, // Userdata by its type.
        LuaType.String => StringMeta,
        _ => null,
    };

    internal LuaValue MetaOf(LuaValue v, string name) => MetatableOf(v)?.Get(name) ?? LuaValue.Nil;

    public LuaValue Index(LuaValue target, LuaValue key)
    {
        for (var loop = 0; loop < 100; loop++)
        {
            LuaValue handler;
            if (target.AsTable is { } table)
            {
                var raw = table.Get(key);
                if (!raw.IsNil || table.Metatable is null)
                {
                    return raw;
                }

                handler = table.Metatable.Get("__index");
                if (handler.IsNil)
                {
                    return raw;
                }
            }
            else
            {
                handler = MetaOf(target, "__index");
                if (handler.IsNil)
                {
                    throw Error($"attempt to index a {TypeName(target)} value{Describe(key)}");
                }
            }

            if (handler.Type == LuaType.Function)
            {
                return First(Call(handler, [target, key]));
            }

            target = handler;
        }

        throw Error("'__index' chain too long; possible loop");
    }

    public void SetIndex(LuaValue target, LuaValue key, LuaValue value)
    {
        for (var loop = 0; loop < 100; loop++)
        {
            LuaValue handler;
            if (target.AsTable is { } table)
            {
                if (table.Metatable is null || !table.Get(key).IsNil || (handler = table.Metatable.Get("__newindex")).IsNil)
                {
                    try
                    {
                        table.Set(key, value);
                    }
                    catch (LuaException e) when (!e.Placed)
                    {
                        throw Placed(e.Value);
                    }

                    return;
                }
            }
            else
            {
                handler = MetaOf(target, "__newindex");
                if (handler.IsNil)
                {
                    throw Error($"attempt to index a {TypeName(target)} value{Describe(key)}");
                }
            }

            if (handler.Type == LuaType.Function)
            {
                Call(handler, [target, key, value]);
                return;
            }

            target = handler;
        }

        throw Error("'__newindex' chain too long; possible loop");
    }

    private static string Describe(LuaValue key) => key.IsString ? $" (field '{key.AsString}')" : "";

    /// <summary>The first of a call's values, or nil when it gave none.</summary>
    public static LuaValue First(LuaValue[] values) => values.Length > 0 ? values[0] : LuaValue.Nil;

    internal LuaValue Arithmetic(Arith op, LuaValue a, LuaValue b)
    {
        if (op >= Arith.BAnd)
        {
            if (Numbers.ToInteger(a) is { } x && Numbers.ToInteger(b) is { } y)
            {
                return LuaValue.Of(Bitwise(op, x, y));
            }
        }
        else
        {
            var (na, nb) = (Numbers.Coerce(a), Numbers.Coerce(b));
            if (na.IsNumber && nb.IsNumber)
            {
                return Numeric(op, na, nb);
            }
        }

        var handler = MetaOf(a, _arithEvents[(int)op]);
        if (handler.IsNil)
        {
            handler = MetaOf(b, _arithEvents[(int)op]);
        }

        if (!handler.IsNil)
        {
            return First(Call(handler, [a, b]));
        }

        var bad = Numbers.Coerce(a).IsNumber ? b : a;
        if (op >= Arith.BAnd && bad.IsNumber)
        {
            throw Error("number has no integer representation");
        }

        throw Error($"attempt to perform {(op >= Arith.BAnd ? "bitwise operation" : "arithmetic")} on a {TypeName(bad)} value");
    }

    private LuaValue Numeric(Arith op, LuaValue a, LuaValue b)
    {
        if (a.IsInteger && b.IsInteger && op is not (Arith.Div or Arith.Pow))
        {
            long x = a.AsInteger, y = b.AsInteger;
            return op switch
            {
                Arith.Add => LuaValue.Of(unchecked(x + y)),
                Arith.Sub => LuaValue.Of(unchecked(x - y)),
                Arith.Mul => LuaValue.Of(unchecked(x * y)),
                Arith.Mod => LuaValue.Of(IntMod(x, y)),
                _ => LuaValue.Of(IntDiv(x, y)),
            };
        }

        double p = a.AsFloat, q = b.AsFloat;
        return op switch
        {
            Arith.Add => LuaValue.Of(p + q),
            Arith.Sub => LuaValue.Of(p - q),
            Arith.Mul => LuaValue.Of(p * q),
            Arith.Div => LuaValue.Of(p / q),
            Arith.Mod => LuaValue.Of(FloatMod(p, q)),
            Arith.Pow => LuaValue.Of(Power(p, q)),
            _ => LuaValue.Of(Math.Floor(p / q)),
        };
    }

    private long IntMod(long x, long y)
    {
        if (y == 0)
        {
            throw Error("attempt to perform 'n%%0'");
        }

        if (y == -1)
        {
            return 0;
        }

        var r = x % y;
        return r != 0 && (r ^ y) < 0 ? r + y : r;
    }

    private long IntDiv(long x, long y)
    {
        if (y == 0)
        {
            throw Error("attempt to perform 'n//0'");
        }

        if (y == -1)
        {
            return unchecked(-x);
        }

        var q = x / y;
        return x % y != 0 && (x ^ y) < 0 ? q - 1 : q;
    }

    private static double FloatMod(double p, double q)
    {
        // C's fmod is exact, as .NET's % on doubles is; Lua then moves the result to the divisor's sign.
        var m = p % q;
        return m != 0 && (m < 0) != (q < 0) ? m + q : m;
    }

    /// <summary>
    /// x^y, worked out the same on every machine: whole powers by repeated
    /// multiplication, a half by the square root; other fractions are
    /// refused, as the C library's pow can differ between systems.
    /// </summary>
    private double Power(double x, double y)
    {
        if (!double.IsFinite(x) || !double.IsFinite(y) || x == 0 || x == 1)
        {
            // These cases are fixed by IEEE 754 and come out the same everywhere.
            return Math.Pow(x, y);
        }

        if (y == 0.5)
        {
            return Math.Sqrt(x);
        }

        if (Numbers.FloatToInteger(y) is not { } n)
        {
            throw Error("fractional powers are not supported (except 0.5); see docs/modding/lua.md");
        }

        // The exponent's size as unsigned, so even long.MinValue has one.
        var (result, square, e) = (1.0, x, n < 0 ? (ulong)(-(n + 1)) + 1 : (ulong)n);
        while (e > 0)
        {
            if ((e & 1) != 0)
            {
                result *= square;
            }

            square *= square;
            e >>= 1;
        }

        return n < 0 ? 1 / result : result;
    }

    private static long Bitwise(Arith op, long x, long y) => op switch
    {
        Arith.BAnd => x & y,
        Arith.BOr => x | y,
        Arith.BXor => x ^ y,
        Arith.Shl => ShiftLeft(x, y),
        _ => ShiftLeft(x, y == long.MinValue ? long.MaxValue : -y),
    };

    // Lua's shifts are logical, and past 63 places give 0.
    private static long ShiftLeft(long x, long n) =>
        n <= -64 || n >= 64 ? 0 : n >= 0 ? x << (int)n : (long)((ulong)x >> (int)-n);

    internal LuaValue Negate(LuaValue a)
    {
        var n = Numbers.Coerce(a);
        if (n.IsInteger)
        {
            return LuaValue.Of(unchecked(-n.AsInteger));
        }

        if (n.IsFloat)
        {
            return LuaValue.Of(-n.AsFloat);
        }

        var handler = MetaOf(a, "__unm");
        return !handler.IsNil ? First(Call(handler, [a, a])) : throw Error($"attempt to perform arithmetic on a {TypeName(a)} value");
    }

    internal LuaValue BitNot(LuaValue a)
    {
        if (Numbers.ToInteger(a) is { } x)
        {
            return LuaValue.Of(~x);
        }

        var handler = MetaOf(a, "__bnot");
        return !handler.IsNil ? First(Call(handler, [a, a])) : throw Error($"attempt to perform bitwise operation on a {TypeName(a)} value");
    }

    internal LuaValue Length(LuaValue a)
    {
        if (a.IsString)
        {
            return LuaValue.Of((long)a.AsString.Length);
        }

        var handler = MetaOf(a, "__len");
        if (!handler.IsNil)
        {
            return First(Call(handler, [a]));
        }

        return a.AsTable is { } t ? LuaValue.Of(t.Length) : throw Error($"attempt to get length of a {TypeName(a)} value");
    }

    internal LuaValue Concat(LuaValue a, LuaValue b)
    {
        if ((a.IsString || a.IsNumber) && (b.IsString || b.IsNumber))
        {
            return LuaValue.Of(a.ToString() + b.ToString());
        }

        var handler = MetaOf(a, "__concat");
        if (handler.IsNil)
        {
            handler = MetaOf(b, "__concat");
        }

        if (!handler.IsNil)
        {
            return First(Call(handler, [a, b]));
        }

        throw Error($"attempt to concatenate a {TypeName(a.IsString || a.IsNumber ? b : a)} value");
    }

    public bool AreEqual(LuaValue a, LuaValue b)
    {
        if (a.Equals(b))
        {
            return true;
        }

        if (a.AsUserData is { } u && b.AsUserData is { } v)
        {
            // Two views of the same host object are the same thing.
            return ReferenceEquals(u.Host, v.Host) || Equals(u.Host, v.Host);
        }

        if (a.Type != LuaType.Table || b.Type != LuaType.Table)
        {
            return false;
        }

        var handler = MetaOf(a, "__eq");
        if (handler.IsNil)
        {
            handler = MetaOf(b, "__eq");
        }

        return !handler.IsNil && First(Call(handler, [a, b])).IsTruthy;
    }

    internal bool LessThan(LuaValue a, LuaValue b)
    {
        if (a.IsNumber && b.IsNumber)
        {
            return Numbers.Less(a, b);
        }

        if (a.IsString && b.IsString)
        {
            return string.CompareOrdinal(a.AsString, b.AsString) < 0;
        }

        return Compared("__lt", a, b);
    }

    internal bool LessOrEqual(LuaValue a, LuaValue b)
    {
        if (a.IsNumber && b.IsNumber)
        {
            return Numbers.LessOrEqual(a, b);
        }

        if (a.IsString && b.IsString)
        {
            return string.CompareOrdinal(a.AsString, b.AsString) <= 0;
        }

        return Compared("__le", a, b);
    }

    private bool Compared(string name, LuaValue a, LuaValue b)
    {
        var handler = MetaOf(a, name);
        if (handler.IsNil)
        {
            handler = MetaOf(b, name);
        }

        if (!handler.IsNil)
        {
            return First(Call(handler, [a, b])).IsTruthy;
        }

        var (ta, tb) = (TypeName(a), TypeName(b));
        throw Error(ta == tb ? $"attempt to compare two {ta} values" : $"attempt to compare {ta} with {tb}");
    }

    /// <summary>A value as tostring gives it: __tostring, __name, or the plain form.</summary>
    public string ToDisplay(LuaValue v)
    {
        var handler = MetaOf(v, "__tostring");
        if (!handler.IsNil && v.Type is LuaType.Table or LuaType.UserData)
        {
            var text = First(Call(handler, [v]));
            return text.IsString ? text.AsString : text.IsNumber ? text.ToString() : throw Error("'__tostring' must return a string");
        }

        return v.Type switch
        {
            LuaType.Table or LuaType.UserData => (MetaOf(v, "__name") is { IsString: true } name ? name.AsString : TypeName(v)) + ": " + Address(ReferenceOf(v)),
            LuaType.Function => (v.AsFunction is LuaBuiltin ? "builtin: " : "function: ") + Address(v.AsFunction!), // A function by its type.
            _ => v.ToString(),
        };
    }

    private static object ReferenceOf(LuaValue v) => (object?)v.AsTable ?? v.AsUserData!; // A table or userdata by its type.
}
