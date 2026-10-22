using System.Text.Json;

namespace OpenPax.Host;

/// <summary>
/// config.json beside the game: what a copy of the game folder handed to
/// friends should carry with it, the public lobby the PUBLIC LOBBY page
/// connects to and hosts the LAN page asks directly (a VPN that carries no
/// broadcasts). Edited by hand; build.sh writes a default once and keeps it.
/// The player's own settings are in openpax.toml in the user folder.
/// </summary>
internal static class GameConfig
{
    public const string FileName = "config.json";

    /// <summary>The public lobby's address (null when the file or the key is missing) and the LAN page's extra hosts; a broken file says so.</summary>
    public static (string? PublicLobby, List<string> LanHosts) Read(string path, TextWriter errors)
    {
        if (!File.Exists(path))
        {
            return (null, []);
        }

        try
        {
            using var doc = JsonDocument.Parse(File.ReadAllText(path), new JsonDocumentOptions { CommentHandling = JsonCommentHandling.Skip, AllowTrailingCommas = true });
            var root = doc.RootElement;
            var lobby = root.TryGetProperty("publicLobby", out var v) && v.ValueKind == JsonValueKind.String && v.GetString() is { } s && s.Trim().Length > 0 ? s.Trim() : null;
            var hosts = root.TryGetProperty("lanHosts", out var h) && h.ValueKind == JsonValueKind.Array
                ? h.EnumerateArray().Where(e => e.ValueKind == JsonValueKind.String).Select(e => e.GetString()!.Trim()).Where(x => x.Length > 0).ToList() // Not null: a string element.
                : [];
            return (lobby, hosts);
        }
        catch (Exception e) when (e is JsonException or IOException)
        {
            errors.WriteLine(string.Format(Strings.ConfigBroken, path, e.Message));
            return (null, []);
        }
    }
}
