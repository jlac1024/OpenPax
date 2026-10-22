using System.Reflection;

namespace OpenPax.Sim.Saving;

/// <summary>
/// Type names in a save, without assembly versions: "Namespace.Name", and
/// for generic types the definition's name with the arguments in angle
/// brackets. Found again among the assemblies the loader is given.
/// </summary>
internal sealed class SaveTypes
{
    private readonly Assembly[] _assemblies;
    private readonly Dictionary<string, Type> _found = [];

    public SaveTypes(IEnumerable<Assembly> assemblies)
    {
        _assemblies = [.. assemblies.Append(typeof(Game).Assembly).Append(typeof(object).Assembly).Append(typeof(SortedSet<>).Assembly).Distinct()];
    }

    public static string NameOf(Type type)
    {
        if (!type.IsGenericType)
        {
            return type.FullName ?? type.Name;
        }

        var definition = type.GetGenericTypeDefinition();
        return (definition.FullName ?? definition.Name) + "<" + string.Join(",", type.GetGenericArguments().Select(NameOf)) + ">";
    }

    /// <summary>The lists and sets saved as their items rather than their insides.</summary>
    public static bool IsCollection(Type type)
    {
        if (!type.IsGenericType)
        {
            return false;
        }

        var definition = type.GetGenericTypeDefinition();
        return definition == typeof(List<>) || definition == typeof(HashSet<>) || definition == typeof(SortedSet<>);
    }

    public Type Find(string name)
    {
        if (_found.TryGetValue(name, out var known))
        {
            return known;
        }

        var pos = 0;
        var type = Parse(name, ref pos) ?? throw new InvalidDataException(string.Format(Strings.SaveUnknownType, name));
        _found[name] = type;
        return type;
    }

    private Type? Parse(string text, ref int pos)
    {
        var start = pos;
        while (pos < text.Length && text[pos] is not ('<' or '>' or ','))
        {
            pos++;
        }

        var name = text[start..pos];
        if (pos < text.Length && text[pos] == '<')
        {
            pos++;
            var arguments = new List<Type>();
            while (true)
            {
                arguments.Add(Parse(text, ref pos) ?? throw new InvalidDataException(string.Format(Strings.SaveUnknownType, text)));
                if (text[pos] == '>')
                {
                    pos++;
                    break;
                }

                pos++;
            }

            return Lookup(name)?.MakeGenericType([.. arguments]);
        }

        return Lookup(name);
    }

    private Type? Lookup(string name)
    {
        if (name.Length == 0)
        {
            return null;
        }

        foreach (var assembly in _assemblies)
        {
            if (assembly.GetType(name) is { } type)
            {
                return type;
            }
        }

        return null;
    }
}
