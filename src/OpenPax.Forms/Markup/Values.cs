using System.Globalization;
using OpenPax.Forms.Layout;
using OpenPax.Render.TrueColor;

namespace OpenPax.Forms.Markup;

/// <summary>Attribute text to property values: numbers, yes or no, names of choices, margins, colors and grid sizes.</summary>
public static class Values
{
    public static bool TryConvert(string text, Type type, out object? value)
    {
        var target = Nullable.GetUnderlyingType(type) ?? type;
        value = null;
        if (target == typeof(string))
        {
            value = text;
            return true;
        }

        if (target == typeof(int))
        {
            var ok = int.TryParse(text, NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out var i);
            value = i;
            return ok;
        }

        if (target == typeof(bool))
        {
            var ok = bool.TryParse(text, out var b);
            value = b;
            return ok;
        }

        if (target.IsEnum)
        {
            var ok = Enum.TryParse(target, text, ignoreCase: true, out var e);
            value = e;
            return ok;
        }

        if (target == typeof(Thickness))
        {
            var ok = Thickness.TryParse(text, out var t);
            value = t;
            return ok;
        }

        if (target == typeof(Color))
        {
            var ok = Color.TryParse(text, out var c);
            value = c;
            return ok;
        }

        if (target == typeof(IReadOnlyList<GridLength>))
        {
            value = GridLength.ParseList(text);
            return true;
        }

        return false;
    }

    /// <summary>A value of any kind made fit for a property, as bindings need: numbers, text and choices convert between each other.</summary>
    public static object? Coerce(object? value, Type type)
    {
        var target = Nullable.GetUnderlyingType(type) ?? type;
        if (value is null || target.IsInstanceOfType(value))
        {
            return value;
        }

        if (target == typeof(string))
        {
            return Convert.ToString(value, CultureInfo.InvariantCulture);
        }

        if (value is string s && TryConvert(s, type, out var parsed))
        {
            return parsed;
        }

        return value is IConvertible && (target.IsPrimitive || target == typeof(decimal))
            ? Convert.ChangeType(value, target, CultureInfo.InvariantCulture)
            : null;
    }
}
