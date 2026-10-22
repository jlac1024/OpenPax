using System.Reflection;
using System.Text;
using OpenPax.Modding;
using OpenPax.Sim;

namespace ApiDocs;

/// <summary>The API reference as Markdown: the types a mod works with, group by group, each with its members and their doc comments.</summary>
internal static class Reference
{
    // What a mod sees, in the order a reader meets it. A name the assemblies do not have is reported and left out.
    private static readonly (string Title, string Intro, string[] Types)[] _groups =
    [
        ("Writing a mod", "A mod's entry point and what it registers at load.", ["OpenPax.Modding.IMod", "OpenPax.Modding.ModContext", "OpenPax.Modding.ModStore", "OpenPax.Modding.ModRandom", "OpenPax.Modding.ModInfo", "OpenPax.Modding.ModManifest"]),
        ("The game", "The running game, its events and what mods report back to the screens.", ["OpenPax.Sim.Game", "OpenPax.Sim.ModEvent", "OpenPax.Sim.ModEventKind", "OpenPax.Sim.ModFight", "OpenPax.Sim.ColonyRating", "OpenPax.Sim.Fixes", "OpenPax.Sim.GameNotice", "OpenPax.Sim.NoticeKind"]),
        ("The galaxy", "Stars, planets and wormholes.", ["OpenPax.Sim.Galaxy.Universe", "OpenPax.Sim.Galaxy.Star", "OpenPax.Sim.Galaxy.Planet", "OpenPax.Sim.Galaxy.Wormhole", "OpenPax.Sim.Galaxy.ISpaceObject"]),
        ("Players, colonies and ships", "Empires and what they own.", ["OpenPax.Sim.Players.Player", "OpenPax.Sim.Players.Treaty", "OpenPax.Sim.Players.Colony", "OpenPax.Sim.Players.Structure", "OpenPax.Sim.Players.Fleet", "OpenPax.Sim.Players.Design", "OpenPax.Sim.Players.Component", "OpenPax.Sim.Players.Species", "OpenPax.Sim.Players.IncidentKind"]),
        ("Game tables", "The data every game is made from, which ChangeTables may change.", ["OpenPax.Sim.Setup.GameTables", "OpenPax.Sim.Setup.GameSetup", "OpenPax.Sim.Players.TechInfo", "OpenPax.Sim.Players.HullInfo"]),
        ("Loading (the game's side)", "How the game finds and loads mods; a mod does not call these.", ["OpenPax.Modding.ModHost", "OpenPax.Modding.ModCatalog"]),
    ];

    private static readonly HashSet<string> _skipped = ["Equals", "GetHashCode", "ToString", "Deconstruct", "<Clone>$", "PrintMembers", "EqualityContract"];

    public static string Write(TextWriter warnings)
    {
        Assembly[] assemblies = [typeof(IMod).Assembly, typeof(Game).Assembly];
        var docs = new XmlDocs(assemblies);
        var nullability = new NullabilityInfoContext();
        var md = new StringBuilder();
        md.Append("# Mod API reference\n\n");
        md.Append("Written by `tools/ApiDocs` from the game's assemblies and their doc comments each build; do not edit by hand. ");
        md.Append("The guides are in [modding.md](../modding.md). Members marked for mods (`[ModApi]`) come first on `Game`; the rest of its public surface follows, as the game's own screens use it.\n\n");

        var groups = _groups.Select(g => (g.Title, g.Intro, Types: g.Types.Select(n => Find(assemblies, n, warnings)).OfType<Type>().ToList())).ToList();
        md.Append("## Contents\n\n");
        foreach (var (title, _, types) in groups)
        {
            md.Append("- **").Append(title).Append("**: ").Append(string.Join(", ", types.Select(t => $"[{Display(t)}](#{Anchor(t)})"))).Append('\n');
        }

        foreach (var (title, intro, types) in groups)
        {
            md.Append("\n## ").Append(title).Append("\n\n").Append(intro).Append('\n');
            foreach (var type in types)
            {
                WriteType(md, type, docs, nullability);
            }
        }

        return md.ToString();
    }

    private static Type? Find(Assembly[] assemblies, string name, TextWriter warnings)
    {
        var type = assemblies.Select(a => a.GetType(name)).FirstOrDefault(t => t is not null);
        if (type is null)
        {
            warnings.WriteLine("ApiDocs: no type " + name + "; left out.");
        }

        return type;
    }

    private static string Display(Type t) => CSharpNames.Of(t);

    public static string Anchor(Type t) => "type-" + t.Name.ToLowerInvariant().Replace('`', '-');

    private static string Kind(Type t) =>
        t.IsEnum ? "enum"
        : t.IsInterface ? "interface"
        : t.IsValueType ? (t.GetMethod("<Clone>$") is null && t.GetProperty("EqualityContract", BindingFlags.NonPublic | BindingFlags.Instance) is null ? "struct" : "record struct")
        : t.GetMethod("<Clone>$") is not null ? "record"
        : t.IsAbstract && t.IsSealed ? "static class"
        : "class";

    private static void WriteType(StringBuilder md, Type type, XmlDocs docs, NullabilityInfoContext nullability)
    {
        md.Append("\n<a id=\"").Append(Anchor(type)).Append("\"></a>\n\n### ").Append(Display(type)).Append("\n\n");
        md.Append('`').Append(Kind(type)).Append(' ').Append(type.Namespace).Append('.').Append(Display(type)).Append("`\n\n");
        var doc = docs.Of(XmlDocs.IdOf(type));
        if (XmlDocs.Text(doc?.Element("summary")) is { Length: > 0 } summary)
        {
            md.Append(summary).Append("\n\n");
        }

        if (type.IsEnum)
        {
            md.Append("| Value | | Meaning |\n|---|---|---|\n");
            foreach (var f in type.GetFields(BindingFlags.Public | BindingFlags.Static))
            {
                var value = Convert.ToInt64(f.GetRawConstantValue(), System.Globalization.CultureInfo.InvariantCulture);
                md.Append("| `").Append(f.Name).Append("` | ").Append(value.ToString(System.Globalization.CultureInfo.InvariantCulture)).Append(" | ")
                    .Append(Cell(XmlDocs.Text(docs.Of(XmlDocs.IdOf(f))?.Element("summary")))).Append(" |\n");
            }

            return;
        }

        // A record's primary constructor parameters are documented on the type.
        var parameters = doc?.Elements("param").Select(p => (Name: p.Attribute("name")?.Value ?? "", Text: XmlDocs.Text(p))).Where(p => p.Text.Length > 0).ToList() ?? [];
        if (parameters.Count > 0)
        {
            md.Append("| Parameter | Meaning |\n|---|---|\n");
            foreach (var (name, text) in parameters)
            {
                md.Append("| `").Append(name).Append("` | ").Append(Cell(text)).Append(" |\n");
            }

            md.Append('\n');
        }

        var members = Members(type).ToList();
        if (type == typeof(Game))
        {
            var forMods = members.Where(m => m.GetCustomAttribute<ModApiAttribute>() is not null).ToList();
            WriteMembers(md, "Made for mods", forMods, docs, nullability);
            WriteMembers(md, "Also public", [.. members.Except(forMods)], docs, nullability);
        }
        else
        {
            WriteMembers(md, "Members", members, docs, nullability);
        }
    }

    private static IEnumerable<MemberInfo> Members(Type type)
    {
        const BindingFlags flags = BindingFlags.Public | BindingFlags.Instance | BindingFlags.Static | BindingFlags.DeclaredOnly;
        var ctors = type.IsAbstract && type.IsSealed ? [] : type.GetConstructors(BindingFlags.Public | BindingFlags.Instance).Cast<MemberInfo>();
        var properties = type.GetProperties(flags).Where(p => !_skipped.Contains(p.Name));
        var events = type.GetEvents(flags);
        var fields = type.GetFields(flags).Where(f => !f.IsSpecialName);
        var methods = type.GetMethods(flags).Where(m => !m.IsSpecialName && !_skipped.Contains(m.Name));
        return ctors
            .Concat(properties.OrderBy(p => p.Name, StringComparer.Ordinal))
            .Concat(methods.OrderBy(m => m.Name, StringComparer.Ordinal).ThenBy(m => m.GetParameters().Length))
            .Concat(events.OrderBy(e => e.Name, StringComparer.Ordinal))
            .Concat(fields.OrderBy(f => f.Name, StringComparer.Ordinal));
    }

    private static void WriteMembers(StringBuilder md, string title, IReadOnlyList<MemberInfo> members, XmlDocs docs, NullabilityInfoContext nullability)
    {
        if (members.Count == 0)
        {
            return;
        }

        md.Append("#### ").Append(title).Append("\n\n");
        foreach (var member in members)
        {
            var doc = docs.Of(XmlDocs.IdOf(member));
            md.Append("- `").Append(CSharpNames.Signature(member, nullability)).Append('`');
            var summary = XmlDocs.Text(doc?.Element("summary"));
            if (summary.Length > 0)
            {
                md.Append(": ").Append(summary.Replace("\n\n", " ", StringComparison.Ordinal));
            }

            foreach (var p in doc?.Elements("param") ?? [])
            {
                if (XmlDocs.Text(p) is { Length: > 0 } text)
                {
                    md.Append("\n  - `").Append(p.Attribute("name")?.Value).Append("`: ").Append(text.Replace("\n\n", " ", StringComparison.Ordinal));
                }
            }

            if (XmlDocs.Text(doc?.Element("returns")) is { Length: > 0 } returns)
            {
                md.Append("\n  - Returns: ").Append(returns.Replace("\n\n", " ", StringComparison.Ordinal));
            }

            md.Append('\n');
        }

        md.Append('\n');
    }

    private static string Cell(string text) => text.Replace("\n\n", " ", StringComparison.Ordinal).Replace("|", "\\|", StringComparison.Ordinal);
}
