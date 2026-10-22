namespace OpenPax.Data.Install;

/// <summary>
/// Where a GOG install of the game is usually found on each system
/// (docs/first-run.md): GOG's own folders on Windows, and on macOS and Linux
/// the folders GOG's installer, innoextract, Wine, Lutris and CrossOver
/// leave it in. Only folders that hold the game are given.
/// </summary>
public static class InstallFinder
{
    public static IReadOnlyList<string> Find()
    {
        var home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        var places = new List<string>();
        if (OperatingSystem.IsWindows())
        {
            foreach (var drive in DriveInfo.GetDrives().Where(d => d.DriveType == DriveType.Fixed).Select(d => d.RootDirectory.FullName))
            {
                places.Add(Path.Combine(drive, "GOG Games"));
                places.Add(Path.Combine(drive, "Program Files (x86)", "GOG Galaxy", "Games"));
                places.Add(Path.Combine(drive, "Program Files", "GOG Galaxy", "Games"));
                places.Add(Path.Combine(drive, "Games"));
            }
        }
        else
        {
            places.Add(Path.Combine(home, "GOG Games"));
            places.Add(Path.Combine(home, "Games"));
            places.Add(Path.Combine(home, ".wine", "drive_c", "GOG Games"));
            places.Add(Path.Combine(home, ".wine", "drive_c", "Program Files (x86)", "GOG Galaxy", "Games"));
            places.AddRange(Under(Path.Combine(home, "Games"), "drive_c", "GOG Games"));
            places.AddRange(Under(Path.Combine(home, "Library", "Application Support", "CrossOver", "Bottles"), "drive_c", "GOG Games"));
        }

        IEnumerable<string> folders = places
            .SelectMany(p => new[] { p, Path.Combine(p, GameInstall.FolderName) })
            .Where(Directory.Exists)
            .Select(GameInstall.Resolve)
            .OfType<string>();
        var installers = Downloaded(InstallerKind.Offline);
        return [.. folders.Concat(installers).Distinct(StringComparer.OrdinalIgnoreCase)];
    }

    /// <summary>
    /// Whether GOG's Galaxy stub sits in the player's downloads, so the setup
    /// can tell them it holds no game when nothing else was found.
    /// </summary>
    public static bool GalaxyStubDownloaded() => Downloaded(InstallerKind.GalaxyStub).Any();

    /// <summary>GOG installers of the given kind in the player's Downloads and Desktop folders.</summary>
    private static IEnumerable<string> Downloaded(InstallerKind kind)
    {
        var home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        foreach (var folder in new[] { Path.Combine(home, "Downloads"), Path.Combine(home, "Desktop") })
        {
            string[] files;
            try
            {
                files = Directory.Exists(folder) ? Directory.GetFiles(folder, "*.exe") : [];
            }
            catch (Exception e) when (e is IOException or UnauthorizedAccessException)
            {
                continue;
            }

            // GOG names both after the game; anything else is not looked into.
            foreach (var file in files.Where(f => System.IO.Path.GetFileName(f).Contains("pax_imperia", StringComparison.OrdinalIgnoreCase)).Order(StringComparer.Ordinal))
            {
                if (InstallerCheck.Identify(file) == kind)
                {
                    yield return file;
                }
            }
        }
    }

    /// <summary>Each folder's own subfolders' path below it (a Lutris or CrossOver prefix per game).</summary>
    private static IEnumerable<string> Under(string folder, params string[] rest)
    {
        if (!Directory.Exists(folder))
        {
            return [];
        }

        try
        {
            return Directory.GetDirectories(folder).Select(d => Path.Combine([d, .. rest]));
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            return [];
        }
    }
}
