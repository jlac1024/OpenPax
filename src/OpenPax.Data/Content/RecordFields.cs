using System.Globalization;
using System.Reflection;
using System.Text;

namespace OpenPax.Data.Content;

/// <summary>
/// A positional record made again with some of its fields changed, by
/// their names in snake case ("damage_by_range" for DamageByRange): how a
/// content file changes a tech or a hull. Values come as TOML gives them
/// (long, double, bool, string, lists) and are fitted to the field's type,
/// with a plain message when one does not fit.
/// </summary>
public static class RecordFields
{
    /// <summary>The fields a record can be given, as snake case names with their C# types.</summary>
    public static IReadOnlyList<(string Name, Type Type)> Of(Type record) =>
        [.. Constructor(record).GetParameters().Select(p => (Snake(p.Name ?? ""), p.ParameterType))];

    /// <summary>The record with the named fields changed; names it does not have are left for the caller to report.</summary>
    public static T With<T>(T record, IReadOnlyDictionary<string, object> values, ICollection<string> used)
        where T : notnull
    {
        var type = record.GetType();
        var ctor = Constructor(type);
        var args = new List<object?>();
        foreach (var p in ctor.GetParameters())
        {
            var name = Snake(p.Name ?? "");
            var current = type.GetProperty(p.Name ?? "")?.GetValue(record);
            if (values.TryGetValue(name, out var given))
            {
                used.Add(name);
                args.Add(Fit(given, p.ParameterType, name));
            }
            else
            {
                args.Add(current);
            }
        }

        return (T)ctor.Invoke([.. args]);
    }

    /// <summary>CamelCase to snake_case: DamageByRange to damage_by_range, Unknown5C to unknown5_c.</summary>
    public static string Snake(string name)
    {
        var s = new StringBuilder();
        for (var i = 0; i < name.Length; i++)
        {
            if (char.IsUpper(name[i]) && i > 0)
            {
                s.Append('_');
            }

            s.Append(char.ToLowerInvariant(name[i]));
        }

        return s.ToString();
    }

    // The primary constructor: the one with the most parameters.
    private static ConstructorInfo Constructor(Type record) => record.GetConstructors().OrderByDescending(c => c.GetParameters().Length).First();

    private static object Fit(object value, Type type, string name)
    {
        try
        {
            if (type == typeof(int))
            {
                return checked((int)Whole(value));
            }

            if (type == typeof(short))
            {
                return checked((short)Whole(value));
            }

            if (type == typeof(byte))
            {
                return checked((byte)Whole(value));
            }

            if (type == typeof(long))
            {
                return Whole(value);
            }

            if (type == typeof(double))
            {
                return value is long l ? l : (double)value;
            }

            if (type == typeof(bool))
            {
                return (bool)value;
            }

            if (type == typeof(string))
            {
                return (string)value;
            }

            if (type.IsGenericType && type.GetGenericTypeDefinition() == typeof(IReadOnlyList<>) && value is List<object> list)
            {
                var element = type.GetGenericArguments()[0];
                var array = Array.CreateInstance(element, list.Count);
                for (var i = 0; i < list.Count; i++)
                {
                    array.SetValue(Fit(list[i], element, name), i);
                }

                return array;
            }
        }
        catch (Exception e) when (e is InvalidCastException or OverflowException)
        {
            throw new FormatException(string.Format(CultureInfo.InvariantCulture, Strings.ContentBadValue, name, Describe(type)));
        }

        throw new FormatException(string.Format(CultureInfo.InvariantCulture, Strings.ContentBadValue, name, Describe(type)));
    }

    private static long Whole(object value) => value switch
    {
        long l => l,
        double d when d == Math.Floor(d) => (long)d,
        _ => throw new InvalidCastException(),
    };

    /// <summary>A field's type as the content docs name it.</summary>
    public static string Describe(Type type) =>
        type == typeof(int) || type == typeof(long) ? "whole number"
        : type == typeof(short) ? "whole number, -32768 to 32767"
        : type == typeof(byte) ? "whole number, 0 to 255"
        : type == typeof(double) ? "number"
        : type == typeof(bool) ? "true or false"
        : type == typeof(string) ? "text"
        : type.IsGenericType ? "list of " + Describe(type.GetGenericArguments()[0]) + "s"
        : type.Name;
}
