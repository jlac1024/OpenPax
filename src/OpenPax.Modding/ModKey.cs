namespace OpenPax.Modding;

/// <summary>
/// A mod as a mod set names it: id, version and the start of its files'
/// hash, written "id@version#hash" (12 hex digits). Two players hold the same
/// mod only when all three match.
/// </summary>
public sealed record ModKey(string Id, string Version, string Hash)
{
    public const int HashDigits = 12;

    public static ModKey Of(ModInfo mod) => new(mod.Manifest.Id, mod.Manifest.Version, mod.Hash[..Math.Min(HashDigits, mod.Hash.Length)]);

    public override string ToString() => Id + "@" + Version + "#" + Hash;

    /// <summary>A mod set as the lobby and the game server carry it: keys in load order, joined by commas. Badly formed entries are left out.</summary>
    public static IReadOnlyList<ModKey> ParseSet(string set) =>
        [.. set.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).Select(Parse).OfType<ModKey>()];

    public static string Join(IEnumerable<ModKey> keys) => string.Join(',', keys);

    public static ModKey? Parse(string text)
    {
        var at = text.IndexOf('@', StringComparison.Ordinal);
        var hash = text.LastIndexOf('#');
        return at <= 0 || hash < at ? null : new ModKey(text[..at], text[(at + 1)..hash], text[(hash + 1)..]);
    }

    /// <summary>Whether a mod found here is this one.</summary>
    public bool Matches(ModInfo mod) => mod.Manifest.Id == Id && mod.Manifest.Version == Version && mod.Hash.StartsWith(Hash, StringComparison.Ordinal);
}
