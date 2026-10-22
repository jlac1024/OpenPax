namespace OpenPax.Host;

/// <summary>
/// Where the player's config and saves live: the per-user folder SDL gives,
/// or the dev folder run.sh names, so dev runs never touch the real one.
/// </summary>
internal static class UserFolder
{
    public const string DevVariable = "OPENPAX_DEV_DIR";

    public static string? Resolve()
    {
        if (Environment.GetEnvironmentVariable(DevVariable) is { Length: > 0 } dev)
        {
            Directory.CreateDirectory(dev);
            return dev;
        }

        var folder = Sdl.Sdl.GetPrefPath(Strings.Organization, Strings.WindowTitle);
        if (folder is not null)
        {
            CarryOver(folder);
        }

        return folder;
    }

    // The project was called Heliograph before it went public as OpenPax.
    private const string OldName = "Heliograph";

    /// <summary>
    /// The first time the new user folder is used, what the old one held (the
    /// config, saves, mods, designed species) is copied in: the folder named
    /// for the old name beside it, as SDL's preference path names folders
    /// (organization, then application). The old one is left as it was.
    /// </summary>
    private static void CarryOver(string folder)
    {
        var app = Path.GetDirectoryName(Path.TrimEndingDirectorySeparator(folder));
        var root = app is null ? null : Path.GetDirectoryName(app);
        if (root is null || Directory.EnumerateFileSystemEntries(folder).Any())
        {
            return;
        }

        var old = Path.Combine(root, OldName, OldName);
        if (!Directory.Exists(old))
        {
            return;
        }

        try
        {
            foreach (var file in Directory.EnumerateFiles(old, "*", SearchOption.AllDirectories))
            {
                var target = Path.Combine(folder, Path.GetRelativePath(old, file));
                Directory.CreateDirectory(Path.GetDirectoryName(target) ?? folder);
                File.Copy(file, target, overwrite: false);
            }

            // The config file had the old name too.
            var config = Path.Combine(folder, "heliograph.toml");
            if (File.Exists(config) && !File.Exists(Path.Combine(folder, ConfigFile.FileName)))
            {
                File.Move(config, Path.Combine(folder, ConfigFile.FileName));
            }
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            Console.Error.WriteLine(string.Format(System.Globalization.CultureInfo.InvariantCulture, Strings.CarryOverFailed, old, e.Message));
        }
    }
}
