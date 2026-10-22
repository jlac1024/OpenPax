using System.Text.RegularExpressions;

namespace OpenPax.Sim.Saving;

/// <summary>
/// The project moved from British to American spelling (decisions.md); a
/// save names its types and fields, so names read from one written before
/// are put into the new spelling and still find their fields.
/// </summary>
public static partial class Spelling
{
    private static readonly (string British, string American)[] _words =
    [
        ("Colour", "Color"), ("colour", "color"), ("Centre", "Center"), ("centre", "center"),
        ("Defence", "Defense"), ("defence", "defense"), ("Neighbour", "Neighbor"), ("neighbour", "neighbor"),
        ("Armour", "Armor"), ("armour", "armor"), ("Favour", "Favor"), ("favour", "favor"),
        ("Grey", "Gray"), ("grey", "gray"), ("Initialis", "Initializ"), ("initialis", "initializ"),
    ];

    // The project's namespaces before it went public as OpenPax.
    private const string OldNamespace = "Heliograph.";
    private const string Namespace = "OpenPax.";

    /// <summary>A name read from a save as the project names it now: American spelling, and the OpenPax namespaces for a save made before the rename.</summary>
    public static string Current(string name) => American(name).Replace(OldNamespace, Namespace, StringComparison.Ordinal);

    public static string American(string name)
    {
        if (!Quick().IsMatch(name))
        {
            return name;
        }

        foreach (var (british, american) in _words)
        {
            name = name.Replace(british, american, StringComparison.Ordinal);
        }

        return name;
    }

    [GeneratedRegex("olour|entre|efence|eighbour|rmour|avour|rey|nitialis")]
    private static partial Regex Quick();
}
