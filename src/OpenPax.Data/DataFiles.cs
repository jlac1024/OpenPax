namespace OpenPax.Data;

/// <summary>
/// The game's data folder with mods' data packs over it (docs/modding.md):
/// a file is looked for in each pack, highest priority first, then in the
/// game's own folder. A pack replaces a file by holding one at the same
/// place, or adds one the game does not have.
/// </summary>
public sealed class DataFiles
{
    /// <param name="baseFolder">The game's own data folder.</param>
    /// <param name="overlays">Data pack folders, the one that wins first.</param>
    public DataFiles(string baseFolder, IEnumerable<string>? overlays = null)
    {
        Base = baseFolder;
        Overlays = [.. overlays ?? []];
    }

    public string Base { get; }

    /// <summary>The data packs over the base, the one that wins first.</summary>
    public IReadOnlyList<string> Overlays { get; }

    /// <summary>Where a file is: the first pack that has it, else the game's own place for it (which may not exist).</summary>
    public string Path(params string[] parts)
    {
        var root = RootIndex(parts);
        return System.IO.Path.Combine([root >= 0 && root < Overlays.Count ? Overlays[root] : Base, .. parts]);
    }

    /// <summary>
    /// Which folder a file is found in: 0 and up for the packs in priority
    /// order, the pack count for the game's own folder, -1 for none. A lower
    /// number wins.
    /// </summary>
    public int RootIndex(params string[] parts)
    {
        for (var i = 0; i < Overlays.Count; i++)
        {
            if (File.Exists(System.IO.Path.Combine([Overlays[i], .. parts])))
            {
                return i;
            }
        }

        return File.Exists(System.IO.Path.Combine([Base, .. parts])) ? Overlays.Count : -1;
    }

    /// <summary>
    /// Every file under a folder, packs and base together, by its path from
    /// that folder: where two hold the same, the winning one.
    /// </summary>
    public IReadOnlyDictionary<string, string> FilesUnder(params string[] folder)
    {
        var found = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        // The base first, then the packs from the lowest priority up, so the winner is written last.
        foreach (var root in Overlays.Reverse().Prepend(Base))
        {
            var dir = System.IO.Path.Combine([root, .. folder]);
            if (!Directory.Exists(dir))
            {
                continue;
            }

            foreach (var file in Directory.EnumerateFiles(dir, "*", SearchOption.AllDirectories))
            {
                found[System.IO.Path.GetRelativePath(dir, file)] = file;
            }
        }

        return found;
    }
}
