namespace OpenPax.Modding;

/// <summary>The mods in the mods folders: each a folder with a mod.toml.</summary>
public static class ModCatalog
{
    /// <summary>Every readable mod, by folder name order; what could not be read is said in the errors.</summary>
    public static IReadOnlyList<ModInfo> Find(string modsFolder, ICollection<string> errors) => Find([modsFolder], errors);

    /// <summary>
    /// Every readable mod in several mods folders, the first folder first. A
    /// mod in an earlier folder hides one with the same id in a later one, so
    /// the player's own copy of a mod the game ships with is the one used.
    /// </summary>
    /// <param name="keepDuplicates">Keep every copy of an id, from every folder, first folder first: to find one by its hash (a room's mod set).</param>
    public static IReadOnlyList<ModInfo> Find(IReadOnlyList<string> modsFolders, ICollection<string> errors, bool keepDuplicates = false)
    {
        var found = new List<ModInfo>();
        foreach (var folder in modsFolders)
        {
            var earlier = found.Count;
            foreach (var mod in FindIn(folder, errors, keepDuplicates))
            {
                if (keepDuplicates || !found.Take(earlier).Any(m => m.Manifest.Id.Equals(mod.Manifest.Id, StringComparison.OrdinalIgnoreCase)))
                {
                    found.Add(mod);
                }
            }
        }

        return found;
    }

    private static List<ModInfo> FindIn(string modsFolder, ICollection<string> errors, bool keepDuplicates)
    {
        var found = new List<ModInfo>();
        if (!Directory.Exists(modsFolder))
        {
            return found;
        }

        foreach (var dir in Directory.EnumerateDirectories(modsFolder).Order(StringComparer.OrdinalIgnoreCase))
        {
            var manifest = Path.Combine(dir, ModManifest.FileName);
            if (!File.Exists(manifest))
            {
                continue;
            }

            try
            {
                var mod = new ModInfo(ModManifest.Parse(File.ReadAllText(manifest)), dir);
                if (!keepDuplicates && found.Any(m => m.Manifest.Id.Equals(mod.Manifest.Id, StringComparison.OrdinalIgnoreCase)))
                {
                    errors.Add(string.Format(System.Globalization.CultureInfo.InvariantCulture, Strings.Duplicate, dir, mod.Manifest.Id));
                    continue;
                }

                found.Add(mod);
            }
            catch (Exception e) when (e is FormatException or IOException or UnauthorizedAccessException)
            {
                errors.Add(string.Format(System.Globalization.CultureInfo.InvariantCulture, Strings.Unreadable, dir, e.Message));
            }
        }

        return found;
    }
}
