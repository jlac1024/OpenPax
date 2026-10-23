using System.Globalization;
using System.Text;
using OpenPax.Data.Toml;
using OpenPax.Ui;

namespace OpenPax.Host;

/// <summary>
/// The config file, openpax.toml in the user folder: the preferences
/// under [preferences] as whole numbers, under [player] the player's name,
/// and under [lobby] the private lobby last typed. Missing on first run, it is
/// written with the game's shipped settings (Data\PrefData) over the
/// defaults; it is written again within a second of a change and when the
/// game closes (the original writes PrefData on closing), so a name or a
/// setting survives a game that is killed.
/// </summary>
internal static class ConfigFile
{
    public const string FileName = "openpax.toml";
    private const string Table = "preferences";
    private const string LobbyTable = "lobby";
    private const string PlayerTable = "player";
    private const string ModsTable = "mods";

    /// <summary>The mods turned on, in their order, from the file; none when it is missing or unreadable (docs/modding.md).</summary>
    public static List<string> EnabledMods(string path)
    {
        try
        {
            return File.Exists(path) && TomlReader.Parse(File.ReadAllText(path)).GetValueOrDefault(ModsTable) is Dictionary<string, object> mods
                && mods.GetValueOrDefault("enabled") is List<object> list
                ? [.. list.OfType<string>().Where(s => s.Length > 0)]
                : [];
        }
        catch (Exception e) when (e is FormatException or IOException)
        {
            return [];
        }
    }

    /// <summary>
    /// The whole-number settings under [preferences], read before the rest
    /// of the game is up (the window's place is needed to open it); none
    /// when the file is missing or unreadable.
    /// </summary>
    public static Dictionary<string, int> EarlyPreferences(string? path)
    {
        try
        {
            return path is not null && File.Exists(path) && TomlReader.Parse(File.ReadAllText(path)).GetValueOrDefault(Table) is Dictionary<string, object> table
                ? table.Where(kv => kv.Value is long l && l is >= int.MinValue and <= int.MaxValue).ToDictionary(kv => kv.Key, kv => (int)(long)kv.Value)
                : [];
        }
        catch (Exception e) when (e is FormatException or IOException)
        {
            return [];
        }
    }

    /// <summary>Reads the file into the preferences; a missing or broken file leaves the defaults and says so for a broken one.</summary>
    public static void Load(string path, string dataFolder, UiApp app, TextWriter errors)
    {
        var preferences = app.Preferences;
        foreach (var name in Preferences.Defaults.Keys)
        {
            preferences.Get(name);
        }

        if (!File.Exists(path))
        {
            var shipped = Path.Combine(dataFolder, "original", "Data", "PrefData");
            if (File.Exists(shipped))
            {
                preferences.Load(OpenPax.Data.Tables.Preferences.Parse(File.ReadAllBytes(shipped))
                    .Where(kv => kv.Value is int or bool)
                    .Select(kv => KeyValuePair.Create(kv.Key, kv.Value is bool b ? (b ? 1 : 0) : (int)kv.Value)));
            }

            Save(path, app);
            return;
        }

        try
        {
            var root = TomlReader.Parse(File.ReadAllText(path));
            if (root.TryGetValue(Table, out var t) && t is Dictionary<string, object> table)
            {
                preferences.Load(table.Where(kv => kv.Value is long).Select(kv => KeyValuePair.Create(kv.Key, checked((int)(long)kv.Value))));
            }

            // The name was kept under [lobby] before Options came; read there when [player] has none.
            var player = root.GetValueOrDefault(PlayerTable) as Dictionary<string, object>;
            var lobby = root.GetValueOrDefault(LobbyTable) as Dictionary<string, object>;
            if ((player?.GetValueOrDefault("name") ?? lobby?.GetValueOrDefault("name")) is string name && name.Trim().Length > 0)
            {
                app.PlayerName = name.Trim();
            }

            if (lobby?.GetValueOrDefault("private") is string address)
            {
                app.LobbyAddress = address.Trim();
            }
        }
        catch (Exception e) when (e is FormatException or OverflowException or IOException)
        {
            errors.WriteLine(string.Format(Strings.ConfigBroken, path, e.Message));
        }
    }

    /// <summary>Writes the file through a temporary one beside it, so a game stopped mid-write never leaves half a file.</summary>
    public static void Save(string path, UiApp app)
    {
        var temporary = path + ".new";
        File.WriteAllText(temporary, Text(app));
        File.Move(temporary, path, overwrite: true);
    }

    /// <summary>The file's text for the settings as they stand; the host writes it when this changes.</summary>
    public static string Text(UiApp app)
    {
        var preferences = app.Preferences;
        var text = new StringBuilder();
        text.Append("# OpenPax settings. Written when they change and when the game closes.\n\n[").Append(Table).Append("]\n");
        // Names that are not plain TOML keys (the original's window ids such
        // as "10,002") are not used by the port and are left out.
        foreach (var (name, value) in preferences.Values.Where(kv => IsBareKey(kv.Key)).OrderBy(kv => kv.Key, StringComparer.Ordinal))
        {
            text.Append(name).Append(" = ").Append(value.ToString(CultureInfo.InvariantCulture)).Append('\n');
        }

        text.Append("\n[").Append(PlayerTable).Append("]\nname = ").Append(Quoted(app.PlayerName)).Append('\n');
        text.Append("\n[").Append(LobbyTable).Append("]\nprivate = ").Append(Quoted(app.LobbyAddress)).Append('\n');
        text.Append("\n[").Append(ModsTable).Append("]\nenabled = [").Append(string.Join(", ", app.EnabledMods.Select(Quoted))).Append("]\n");
        return text.ToString();
    }

    private static string Quoted(string text)
    {
        var quoted = new StringBuilder("\"");
        foreach (var c in text)
        {
            _ = c switch
            {
                '"' => quoted.Append("\\\""),
                '\\' => quoted.Append("\\\\"),
                < ' ' => quoted, // A name typed on the lobby screen has none; our reader takes no \u escapes.
                _ => quoted.Append(c),
            };
        }

        return quoted.Append('"').ToString();
    }

    private static bool IsBareKey(string name)
    {
        return name.Length > 0 && name.All(c => char.IsAsciiLetterOrDigit(c) || c is '_' or '-');
    }
}
