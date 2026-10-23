namespace OpenPax.TestSupport;

/// <summary>
/// Finds your copy of the game for tests that need its files (README,
/// building from source): OPENPAX_GAME, from the environment or from
/// build/local.env as build.sh reads it, naming a GOG game folder, a folder
/// holding one, or GOG's offline installer. Those tests skip with a clear
/// message without it.
/// </summary>
internal static class GameData
{
    public const string FolderName = "Pax Imperia - Eminent Domain";
    private const string Executable = "Pax Imperia.exe";
    private const string SettingName = "OPENPAX_GAME";

    /// <summary>The game folder (the one holding Pax Imperia.exe).</summary>
    public static string RequireFolder()
    {
        var folder = Folder();
        Assert.SkipWhen(folder is null, $"The game's files are not set: point {SettingName} (or build/local.env) at your GOG copy of the game");

        // SkipWhen throws when folder is null, so it is set here.
        return folder!;
    }

    /// <summary>GOG's installers matching a name, in the place the setting names and beside the game folder.</summary>
    public static string[] Installers(string pattern)
    {
        if (Setting() is not { } place)
        {
            return [];
        }

        var folders = new List<string>();
        if (File.Exists(place) && Path.GetDirectoryName(place) is { } holder)
        {
            folders.Add(holder);
        }
        else if (Directory.Exists(place))
        {
            folders.Add(place);
            if (Folder() is { } game && Path.GetDirectoryName(game) is { } parent)
            {
                folders.Add(parent);
            }
        }

        return [.. folders.Distinct().SelectMany(f => Directory.GetFiles(f, pattern)).Distinct().Order(StringComparer.Ordinal)];
    }

    private static string? Folder()
    {
        if (Setting() is not { } place || !Directory.Exists(place))
        {
            return null;
        }

        if (File.Exists(Path.Combine(place, Executable)))
        {
            return Path.GetFullPath(place);
        }

        var inside = Path.Combine(place, FolderName);
        return File.Exists(Path.Combine(inside, Executable)) ? Path.GetFullPath(inside) : null;
    }

    // The environment first, as build.sh exports it; else build/local.env, so
    // a plain `dotnet test` finds it too. "$ROOT" there is the repository.
    private static string? Setting()
    {
        if (Environment.GetEnvironmentVariable(SettingName) is { Length: > 0 } set)
        {
            return set;
        }

        var root = Root();
        var file = root is null ? null : Path.Combine(root, "build", "local.env");
        if (file is null || !File.Exists(file))
        {
            return null;
        }

        foreach (var line in File.ReadAllLines(file))
        {
            var text = line.Trim();
            if (text.StartsWith(SettingName + "=", StringComparison.Ordinal))
            {
                var value = text[(SettingName.Length + 1)..].Trim().Trim('"', '\'');
                return value.Replace("$ROOT", root, StringComparison.Ordinal).Replace("${ROOT}", root, StringComparison.Ordinal);
            }
        }

        return null;
    }

    /// <summary>The repository's own folder (the one with OpenPax.slnx), for what tests write under build/.</summary>
    public static string RepositoryRoot() => Root() ?? throw new InvalidOperationException("OpenPax.slnx not found above the tests");

    private static string? Root()
    {
        for (var dir = new DirectoryInfo(AppContext.BaseDirectory); dir is not null; dir = dir.Parent)
        {
            if (File.Exists(Path.Combine(dir.FullName, "OpenPax.slnx")))
            {
                return dir.FullName;
            }
        }

        return null;
    }
}
