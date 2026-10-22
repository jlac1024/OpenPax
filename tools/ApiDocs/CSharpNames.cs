using System.Reflection;

namespace ApiDocs;

/// <summary>Types and signatures as a C# reader writes them: int, string?, IReadOnlyList&lt;Fleet&gt;.</summary>
internal static class CSharpNames
{
    private static readonly Dictionary<Type, string> _keywords = new()
    {
        [typeof(void)] = "void",
        [typeof(bool)] = "bool",
        [typeof(byte)] = "byte",
        [typeof(sbyte)] = "sbyte",
        [typeof(short)] = "short",
        [typeof(ushort)] = "ushort",
        [typeof(int)] = "int",
        [typeof(uint)] = "uint",
        [typeof(long)] = "long",
        [typeof(ulong)] = "ulong",
        [typeof(float)] = "float",
        [typeof(double)] = "double",
        [typeof(decimal)] = "decimal",
        [typeof(char)] = "char",
        [typeof(string)] = "string",
        [typeof(object)] = "object",
    };

    public static string Of(Type type, NullabilityInfo? nullability = null)
    {
        if (Nullable.GetUnderlyingType(type) is { } inner)
        {
            return Of(inner) + "?";
        }

        var nullable = nullability is { ReadState: NullabilityState.Nullable } && !type.IsValueType ? "?" : "";
        if (_keywords.TryGetValue(type, out var keyword))
        {
            return keyword + nullable;
        }

        if (type.IsArray)
        {
            return Of(type.GetElementType()!, nullability?.ElementType) + "[]" + nullable; // an array type has an element type
        }

        if (type.IsByRef)
        {
            return "ref " + Of(type.GetElementType()!); // a by-ref type has an element type
        }

        if (type.IsGenericParameter)
        {
            return type.Name;
        }

        var name = type.Name;
        var tick = name.IndexOf('`', StringComparison.Ordinal);
        if (tick >= 0)
        {
            name = name[..tick];
        }

        if (type.IsGenericType)
        {
            var args = type.GetGenericArguments();
            var argNull = nullability?.GenericTypeArguments;
            var shown = args.Select((a, i) => Of(a, argNull is { } n && i < n.Length ? n[i] : null));

            // Value tuples read as (a, b).
            if (type.FullName?.StartsWith("System.ValueTuple", StringComparison.Ordinal) == true)
            {
                return "(" + string.Join(", ", shown) + ")" + nullable;
            }

            name += "<" + string.Join(", ", shown) + ">";
        }

        return name + nullable;
    }

    /// <summary>A member's line: its signature as declared, without bodies.</summary>
    public static string Signature(MemberInfo member, NullabilityInfoContext context) => member switch
    {
        ConstructorInfo c => "new " + Of(c.DeclaringType!) + "(" + Parameters(c, context) + ")", // a member always has its type
        MethodInfo m => (m.IsStatic ? "static " : "") + Of(m.ReturnType, context.Create(m.ReturnParameter)) + " " + m.Name + Generics(m) + "(" + Parameters(m, context) + ")",
        PropertyInfo p => (p.GetMethod?.IsStatic == true ? "static " : "") + Of(p.PropertyType, context.Create(p)) + " " + p.Name + " { " + Accessors(p) + "}",
        FieldInfo f => (f.IsLiteral ? "const " : f.IsStatic ? "static " : "") + Of(f.FieldType, context.Create(f)) + " " + f.Name + (f.IsLiteral ? " = " + Literal(f.GetRawConstantValue()) : ""),
        EventInfo e => "event " + Of(e.EventHandlerType!, context.Create(e)) + " " + e.Name, // an event has a handler type
        _ => member.Name,
    };

    private static string Literal(object? value) => value switch
    {
        string s => "\"" + s + "\"",
        null => "null",
        IFormattable f => f.ToString(null, System.Globalization.CultureInfo.InvariantCulture),
        _ => value.ToString() ?? "",
    };

    private static string Generics(MethodInfo m) =>
        m.IsGenericMethod ? "<" + string.Join(", ", m.GetGenericArguments().Select(a => a.Name)) + ">" : "";

    private static string Accessors(PropertyInfo p) =>
        (p.GetMethod is { IsPublic: true } ? "get; " : "")
        + (p.SetMethod is { IsPublic: true } set
            ? (set.ReturnParameter.GetRequiredCustomModifiers().Any(t => t.Name == "IsExternalInit") ? "init; " : "set; ")
            : "");

    private static string Parameters(MethodBase method, NullabilityInfoContext context) =>
        string.Join(", ", method.GetParameters().Select(p =>
            Of(p.ParameterType, context.Create(p)) + " " + p.Name + (p.HasDefaultValue ? " = " + Literal(p.DefaultValue) : "")));
}
