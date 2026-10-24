namespace OpenPax.Modding;

/// <summary>A file a mod names, kept inside the mod's own folder.</summary>
internal static class ModFiles
{
    /// <summary>The full path of a file named relative to the mod's folder, or null when the name would lead out of it.</summary>
    public static string? Resolve(string folder, string name)
    {
        if (name.Length == 0 || Path.IsPathRooted(name) || name.Contains(':', StringComparison.Ordinal))
        {
            return null;
        }

        var root = Path.GetFullPath(folder);
        var full = Path.GetFullPath(Path.Combine(root, name.Replace('\\', '/')));
        var inside = root.EndsWith(Path.DirectorySeparatorChar) ? root : root + Path.DirectorySeparatorChar;
        return full.StartsWith(inside, StringComparison.Ordinal) ? full : null;
    }
}
