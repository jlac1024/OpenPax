using System.Globalization;

namespace OpenPax.Lua.Runtime;

/// <summary>
/// Lua 5.4's number rules: integers and floats compared exactly, floats
/// printed as "%.14g" (with ".0" on a whole value), text read as numbers.
/// </summary>
internal static class Numbers
{
    private static readonly CultureInfo _invariant = CultureInfo.InvariantCulture;

    /// <summary>The integer a float equals exactly, or null (a fraction, out of range, NaN).</summary>
    public static long? FloatToInteger(double d)
    {
        if (d >= -9.2233720368547758E18 && d < 9.2233720368547758E18 && Math.Floor(d) == d)
        {
            return (long)d;
        }

        return null;
    }

    public static bool Equal(LuaValue a, LuaValue b)
    {
        if (a.IsInteger && b.IsInteger)
        {
            return a.AsInteger == b.AsInteger;
        }

        if (a.IsInteger)
        {
            return FloatToInteger(b.AsFloat) is { } i && i == a.AsInteger;
        }

        if (b.IsInteger)
        {
            return FloatToInteger(a.AsFloat) is { } i && i == b.AsInteger;
        }

        return a.AsFloat == b.AsFloat;
    }

    /// <summary>a &lt; b for two numbers, exact across integers and floats.</summary>
    public static bool Less(LuaValue a, LuaValue b)
    {
        if (a.IsInteger && b.IsInteger)
        {
            return a.AsInteger < b.AsInteger;
        }

        if (a.IsFloat && b.IsFloat)
        {
            return a.AsFloat < b.AsFloat;
        }

        return a.IsInteger ? IntLessFloat(a.AsInteger, b.AsFloat) : FloatLessInt(a.AsFloat, b.AsInteger);
    }

    public static bool LessOrEqual(LuaValue a, LuaValue b)
    {
        if (a.IsInteger && b.IsInteger)
        {
            return a.AsInteger <= b.AsInteger;
        }

        if (a.IsFloat && b.IsFloat)
        {
            return a.AsFloat <= b.AsFloat;
        }

        return a.IsInteger ? !FloatLessInt(b.AsFloat, a.AsInteger) && !double.IsNaN(b.AsFloat) : !IntLessFloat(b.AsInteger, a.AsFloat) && !double.IsNaN(a.AsFloat);
    }

    // i < f exactly: within range, i < f is i < ceil(f) (for a whole f, f itself).
    private static bool IntLessFloat(long i, double f) =>
        !double.IsNaN(f) && (f >= 9.2233720368547758E18 || (f > -9.2233720368547758E18 && i < (long)Math.Ceiling(f)));

    private static bool FloatLessInt(double f, long i) =>
        !double.IsNaN(f) && (f < -9.2233720368547758E18 || (f < 9.2233720368547758E18 && (long)Math.Floor(f) < i));

    /// <summary>A float as Lua 5.4 prints it: "%.14g", and ".0" when that reads as a whole number.</summary>
    public static string FormatFloat(double d)
    {
        if (double.IsNaN(d))
        {
            return double.IsNegative(d) ? "-nan" : "nan";
        }

        if (double.IsInfinity(d))
        {
            return d > 0 ? "inf" : "-inf";
        }

        var text = Format.G(d, 14, alternate: false, upper: false);
        return text.AsSpan().IndexOfAny(".enia") >= 0 ? text : text + ".0";
    }

    /// <summary>Text as a number, as Lua reads a numeral (surrounding spaces allowed), or nil.</summary>
    public static LuaValue Parse(string text)
    {
        var s = text.Trim(' ', '\t', '\n', '\r', '\f', '\v');
        if (s.Length == 0)
        {
            return LuaValue.Nil;
        }

        var negative = false;
        var body = s;
        if (body[0] is '-' or '+')
        {
            negative = body[0] == '-';
            body = body[1..];
        }

        if (body.StartsWith("0x", StringComparison.OrdinalIgnoreCase))
        {
            var hex = body[2..];
            if (hex.Length > 0 && hex.All(char.IsAsciiHexDigit))
            {
                // Hex integers wrap around, as in Lua.
                ulong value = 0;
                foreach (var c in hex)
                {
                    value = (value << 4) | (uint)Convert.ToInt32(c.ToString(), 16);
                }

                var v = unchecked((long)value);
                return LuaValue.Of(negative ? unchecked(-v) : v);
            }

            return LuaValue.Nil;
        }

        if (body.Length == 0 || !(char.IsAsciiDigit(body[0]) || (body[0] == '.' && body.Length > 1 && char.IsAsciiDigit(body[1]))))
        {
            return LuaValue.Nil;
        }

        if (body.All(char.IsAsciiDigit))
        {
            if (long.TryParse(s, NumberStyles.AllowLeadingSign, _invariant, out var i))
            {
                return LuaValue.Of(i);
            }

            // Too large for an integer: a float, as in Lua.
            return double.TryParse(s, NumberStyles.Float, _invariant, out var big) ? LuaValue.Of(big) : LuaValue.Nil;
        }

        if (body.Any(c => !(char.IsAsciiDigit(c) || c is '.' or 'e' or 'E' or '+' or '-')))
        {
            return LuaValue.Nil;
        }

        return double.TryParse(s, NumberStyles.Float, _invariant, out var d) ? LuaValue.Of(d) : LuaValue.Nil;
    }

    /// <summary>A value as a number for arithmetic: a number, or text that reads as one; else nil.</summary>
    public static LuaValue Coerce(LuaValue v) => v.IsNumber ? v : v.IsString ? Parse(v.AsString) : LuaValue.Nil;

    /// <summary>A value as an integer for the bitwise operators and library functions, or null.</summary>
    public static long? ToInteger(LuaValue v)
    {
        var n = Coerce(v);
        return n.IsInteger ? n.AsInteger : n.IsFloat ? FloatToInteger(n.AsFloat) : null;
    }
}
