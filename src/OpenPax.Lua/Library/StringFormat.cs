using System.Globalization;
using System.Text;
using OpenPax.Lua.Runtime;

namespace OpenPax.Lua.Library;

/// <summary>string.format: C's printf conversions (%d %i %u %c %x %X %o %e %E %f %g %G %q %s %%) with flags, width and precision.</summary>
internal static class StringFormat
{
    private static readonly CultureInfo _invariant = CultureInfo.InvariantCulture;

    public static string Format(LuaState state, LuaValue[] args)
    {
        var format = Args.String(args, 0, "format");
        var text = new StringBuilder();
        var next = 1;
        for (var i = 0; i < format.Length; i++)
        {
            var c = format[i];
            if (c != '%')
            {
                text.Append(c);
                continue;
            }

            if (++i >= format.Length)
            {
                throw new LuaException("invalid conversion '%' to 'format'");
            }

            if (format[i] == '%')
            {
                text.Append('%');
                continue;
            }

            var specStart = i;
            var flags = "";
            while (i < format.Length && "-+ #0".Contains(format[i], StringComparison.Ordinal))
            {
                flags += format[i++];
            }

            var width = Digits(format, ref i);
            int? precision = null;
            if (i < format.Length && format[i] == '.')
            {
                i++;
                precision = Digits(format, ref i) ?? 0;
            }

            if (i >= format.Length)
            {
                throw new LuaException($"invalid conversion '%{format[specStart..]}' to 'format'");
            }

            var conversion = format[i];
            var argument = next++;
            var body = conversion switch
            {
                'd' or 'i' => Integer(args, argument, precision, flags),
                'u' => Integer(args, argument, precision, flags),
                'c' => ((char)Args.Integer(args, argument, "format")).ToString(),
                'x' or 'X' or 'o' => Radix(Args.Integer(args, argument, "format"), conversion, precision, flags),
                'e' or 'E' => Signed(Runtime.Format.E(Args.Number(args, argument, "format").AsFloat, precision ?? 6, conversion == 'E'), flags),
                'f' or 'F' => Signed(Runtime.Format.F(Args.Number(args, argument, "format").AsFloat, precision ?? 6), flags),
                'g' or 'G' => Signed(Runtime.Format.G(Args.Number(args, argument, "format").AsFloat, precision ?? 6, flags.Contains('#', StringComparison.Ordinal), conversion == 'G'), flags),
                'q' => Quoted(Args.At(args, argument)),
                's' => Text(state, args, argument, precision),
                _ => throw new LuaException($"invalid conversion '%{format[specStart..(i + 1)]}' to 'format'"),
            };
            text.Append(Pad(body, width ?? 0, flags, conversion));
        }

        return text.ToString();
    }

    private static int? Digits(string format, ref int i)
    {
        var start = i;
        while (i < format.Length && char.IsAsciiDigit(format[i]) && i - start < 2)
        {
            i++;
        }

        return i > start ? int.Parse(format.AsSpan(start, i - start), _invariant) : null;
    }

    private static string Integer(LuaValue[] args, int argument, int? precision, string flags)
    {
        var n = Args.Integer(args, argument, "format");
        var digits = n == long.MinValue ? "9223372036854775808" : Math.Abs(n).ToString(_invariant);
        if (precision is { } p)
        {
            digits = digits.PadLeft(p, '0');
        }

        return (n < 0 ? "-" : flags.Contains('+', StringComparison.Ordinal) ? "+" : flags.Contains(' ', StringComparison.Ordinal) ? " " : "") + digits;
    }

    private static string Radix(long n, char conversion, int? precision, string flags)
    {
        var digits = conversion == 'o' ? Convert.ToString(n, 8) : n.ToString(conversion == 'x' ? "x" : "X", _invariant);
        if (precision is { } p)
        {
            digits = digits.PadLeft(p, '0');
        }

        if (flags.Contains('#', StringComparison.Ordinal) && n != 0)
        {
            digits = (conversion == 'o' ? "0" : conversion == 'x' ? "0x" : "0X") + digits;
        }

        return digits;
    }

    private static string Signed(string number, string flags) =>
        number.StartsWith('-') ? number : flags.Contains('+', StringComparison.Ordinal) ? "+" + number : flags.Contains(' ', StringComparison.Ordinal) ? " " + number : number;

    private static string Text(LuaState state, LuaValue[] args, int argument, int? precision)
    {
        var s = state.ToDisplay(Args.Any(args, argument, "format"));
        return precision is { } p && p < s.Length ? s[..p] : s;
    }

    private static string Pad(string body, int width, string flags, char conversion)
    {
        if (body.Length >= width)
        {
            return body;
        }

        if (flags.Contains('-', StringComparison.Ordinal))
        {
            return body.PadRight(width);
        }

        // Zero padding goes after the sign, for numbers only.
        if (flags.Contains('0', StringComparison.Ordinal) && conversion is not ('s' or 'q' or 'c'))
        {
            var sign = body.Length > 0 && body[0] is '-' or '+' or ' ' ? body[..1] : "";
            return sign + body[sign.Length..].PadLeft(width - sign.Length, '0');
        }

        return body.PadLeft(width);
    }

    private static string Quoted(LuaValue v)
    {
        if (v.IsString)
        {
            var text = new StringBuilder("\"");
            foreach (var c in v.AsString)
            {
                text.Append(c switch
                {
                    '"' => "\\\"",
                    '\\' => "\\\\",
                    '\n' => "\\n",
                    '\r' => "\\r",
                    '\0' => "\\0",
                    _ when c < 32 || c == 127 => "\\" + ((int)c).ToString(_invariant),
                    _ => c.ToString(),
                });
            }

            return text.Append('"').ToString();
        }

        if (v.IsInteger)
        {
            return v.AsInteger == long.MinValue ? "0x8000000000000000" : v.AsInteger.ToString(_invariant);
        }

        if (v.IsFloat)
        {
            var d = v.AsFloat;
            return d == Math.Floor(d) && double.IsFinite(d) ? d.ToString("R", _invariant) + (d.ToString("R", _invariant).Contains('E', StringComparison.Ordinal) ? "" : ".0") : double.IsNaN(d) ? "(0/0)" : double.IsInfinity(d) ? (d > 0 ? "1e9999" : "-1e9999") : d.ToString("R", _invariant);
        }

        return v.Type is LuaType.Nil or LuaType.Boolean ? v.ToString() : throw new LuaException("bad argument to 'format' (value has no literal form)");
    }
}
