using OpenPax.Data.Toml;

namespace OpenPax.Modding;

/// <summary>
/// A mod's mod.toml (docs/modding.md): who it is, and what it brings: a data
/// pack (its data folder, laid out as the game's), a C# assembly or a Lua
/// script (docs/modding/lua.md), or a pack with either.
/// </summary>
public sealed record ModManifest(string Id, string Name, string Version, string Description, string? Assembly, string? Script = null)
{
    public const string FileName = "mod.toml";

    /// <summary>The folder of a mod's data pack, beside its manifest.</summary>
    public const string DataFolder = "data";

    /// <summary>How a game and the lobby name this mod: id and version.</summary>
    public string Key => Id + "@" + Version;

    /// <summary>A mod's manifest; throws <see cref="FormatException"/> with what is wrong.</summary>
    public static ModManifest Parse(string text)
    {
        var t = TomlReader.Parse(text);
        string? Text(string key) => t.TryGetValue(key, out var v) && v is string s && s.Trim().Length > 0 ? s.Trim() : null;
        var id = Text("id") ?? throw new FormatException(Strings.NoId);
        if (!id.All(c => char.IsAsciiLetterOrDigit(c) || c is '-' or '_' or '.'))
        {
            throw new FormatException(string.Format(System.Globalization.CultureInfo.InvariantCulture, Strings.BadId, id));
        }

        var assembly = Text("assembly");
        var script = Text("script");
        if (assembly is not null && script is not null)
        {
            throw new FormatException(Strings.AssemblyAndScript);
        }

        return new ModManifest(id, Text("name") ?? id, Text("version") ?? "1", Text("description") ?? "", assembly, script);
    }
}
