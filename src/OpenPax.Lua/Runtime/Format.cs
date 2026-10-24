using System.Globalization;

namespace OpenPax.Lua.Runtime;

/// <summary>
/// Floats written as C's printf writes them (%f, %e, %g), which Lua's
/// tostring and string.format follow. .NET's own formats round correctly
/// but differ in the exponent's shape and in %g's choice of form.
/// </summary>
internal static class Format
{
    private static readonly CultureInfo _invariant = CultureInfo.InvariantCulture;

    /// <summary>%.{precision}f</summary>
    public static string F(double d, int precision) => Special(d, false) ?? d.ToString("F" + precision.ToString(_invariant), _invariant);

    /// <summary>%.{precision}e (or %E): at least two exponent digits, as C writes them.</summary>
    public static string E(double d, int precision, bool upper)
    {
        if (Special(d, upper) is { } s)
        {
            return s;
        }

        var text = d.ToString((upper ? "E" : "e") + precision.ToString(_invariant), _invariant);
        var at = text.IndexOfAny(['e', 'E']);
        var mantissa = text[..at];
        var sign = text[at + 1];
        var digits = text[(at + 2)..].TrimStart('0');
        return mantissa + text[at] + sign + digits.PadLeft(2, '0');
    }

    /// <summary>%.{precision}g (or %G): %e or %f by the exponent, trailing zeros taken off unless alternate.</summary>
    public static string G(double d, int precision, bool alternate, bool upper)
    {
        if (Special(d, upper) is { } s)
        {
            return s;
        }

        var p = precision == 0 ? 1 : precision;
        var e = E(d, p - 1, upper);
        var exponent = int.Parse(e[(e.IndexOfAny(['e', 'E']) + 1)..], NumberStyles.AllowLeadingSign, _invariant);
        var text = exponent < -4 || exponent >= p ? e : F(d, p - 1 - exponent);
        if (alternate)
        {
            return text;
        }

        // Without '#', zeros after the point go, and the point with them.
        var mark = text.IndexOfAny(['e', 'E']);
        var (number, tail) = mark >= 0 ? (text[..mark], text[mark..]) : (text, "");
        if (number.Contains('.', StringComparison.Ordinal))
        {
            number = number.TrimEnd('0').TrimEnd('.');
        }

        return number + tail;
    }

    private static string? Special(double d, bool upper)
    {
        if (double.IsNaN(d))
        {
            return (double.IsNegative(d) ? "-" : "") + (upper ? "NAN" : "nan");
        }

        if (double.IsInfinity(d))
        {
            return (d < 0 ? "-" : "") + (upper ? "INF" : "inf");
        }

        return null;
    }
}
