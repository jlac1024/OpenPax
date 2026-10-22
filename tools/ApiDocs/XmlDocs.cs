using System.Reflection;
using System.Text;
using System.Xml.Linq;

namespace ApiDocs;

/// <summary>The doc comments of the assemblies, by member id ("T:", "M:", "P:", "F:", "E:"), read from the XML files the compiler writes beside them.</summary>
internal sealed class XmlDocs
{
    private readonly Dictionary<string, XElement> _members = new(StringComparer.Ordinal);

    public XmlDocs(IEnumerable<Assembly> assemblies)
    {
        foreach (var assembly in assemblies)
        {
            var path = Path.ChangeExtension(assembly.Location, ".xml");
            if (!File.Exists(path))
            {
                continue;
            }

            foreach (var member in XDocument.Load(path).Descendants("member"))
            {
                if (member.Attribute("name")?.Value is { } name)
                {
                    _members[name] = member;
                }
            }
        }
    }

    public XElement? Of(string id) => _members.GetValueOrDefault(id);

    /// <summary>A doc element as Markdown: see and paramref as code, c as code, para as a paragraph break.</summary>
    public static string Text(XElement? element)
    {
        if (element is null)
        {
            return "";
        }

        var text = new StringBuilder();
        foreach (var node in element.Nodes())
        {
            switch (node)
            {
                case XText t:
                    text.Append(t.Value);
                    break;
                case XElement e when e.Name == "see" || e.Name == "seealso":
                    text.Append('`').Append(Short(e.Attribute("cref")?.Value ?? e.Attribute("langword")?.Value ?? e.Value)).Append('`');
                    break;
                case XElement e when e.Name == "paramref" || e.Name == "typeparamref":
                    text.Append('`').Append(e.Attribute("name")?.Value).Append('`');
                    break;
                case XElement e when e.Name == "c":
                    text.Append('`').Append(e.Value).Append('`');
                    break;
                case XElement e when e.Name == "para":
                    text.Append("\n\n").Append(Text(e)).Append("\n\n");
                    break;
                case XElement e:
                    text.Append(Text(e));
                    break;
            }
        }

        // Doc comments wrap at the source's width: one paragraph per blank line.
        var paragraphs = text.ToString().Split("\n\n", StringSplitOptions.RemoveEmptyEntries)
            .Select(p => string.Join(' ', p.Split((char[])['\n', '\r', ' ', '\t'], StringSplitOptions.RemoveEmptyEntries)))
            .Where(p => p.Length > 0);
        return string.Join("\n\n", paragraphs);
    }

    /// <summary>A cref as a reader would write it: "T:OpenPax.Sim.Game" as Game, "M:...Game.Tell(...)" as Game.Tell.</summary>
    public static string Short(string cref)
    {
        var name = cref.Length > 2 && cref[1] == ':' ? cref[2..] : cref;
        var paren = name.IndexOf('(', StringComparison.Ordinal);
        if (paren >= 0)
        {
            name = name[..paren];
        }

        var parts = name.Split('.');
        return parts.Length >= 2 && cref.StartsWith("T:", StringComparison.Ordinal) ? parts[^1]
            : parts.Length >= 2 ? parts[^2] + "." + parts[^1]
            : name;
    }

    // The member ids the compiler writes (the C# spec's documentation comment ids).
    public static string IdOf(Type type) => "T:" + TypeName(type, false);

    public static string IdOf(MemberInfo member) => member switch
    {
        ConstructorInfo c => "M:" + TypeName(c.DeclaringType!, false) + ".#ctor" + Parameters(c), // a member always has its type
        MethodInfo m => "M:" + TypeName(m.DeclaringType!, false) + "." + m.Name + (m.IsGenericMethod ? "``" + m.GetGenericArguments().Length : "") + Parameters(m),
        PropertyInfo p => "P:" + TypeName(p.DeclaringType!, false) + "." + p.Name + (p.GetIndexParameters() is { Length: > 0 } ip ? "(" + string.Join(',', ip.Select(x => TypeName(x.ParameterType, true))) + ")" : ""),
        FieldInfo f => "F:" + TypeName(f.DeclaringType!, false) + "." + f.Name,
        EventInfo e => "E:" + TypeName(e.DeclaringType!, false) + "." + e.Name,
        _ => "",
    };

    private static string Parameters(MethodBase method)
    {
        var ps = method.GetParameters();
        return ps.Length == 0 ? "" : "(" + string.Join(',', ps.Select(p => TypeName(p.ParameterType, true))) + ")";
    }

    private static string TypeName(Type type, bool inSignature)
    {
        if (type.IsByRef)
        {
            return TypeName(type.GetElementType()!, inSignature) + "@"; // a by-ref type has an element type
        }

        if (type.IsArray)
        {
            return TypeName(type.GetElementType()!, inSignature) + "[]"; // an array type has an element type
        }

        if (type.IsGenericParameter)
        {
            return (type.DeclaringMethod is null ? "`" : "``") + type.GenericParameterPosition;
        }

        var name = type.IsNested ? TypeName(type.DeclaringType!, inSignature) + "." + type.Name : type.Namespace + "." + type.Name; // a nested type has a declaring type
        if (!type.IsGenericType)
        {
            return name;
        }

        var tick = name.IndexOf('`', StringComparison.Ordinal);
        if (!inSignature || type.IsGenericTypeDefinition)
        {
            return name;
        }

        return name[..tick] + "{" + string.Join(',', type.GetGenericArguments().Select(a => TypeName(a, true))) + "}";
    }
}
