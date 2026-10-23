namespace OpenPax.Host.Update;

/// <summary>
/// Where the running game is installed: the folder a release bundle unpacks
/// to. On Windows and Linux that is the program's own folder; on macOS the
/// program runs inside OpenPax.app/Contents/MacOS, so it is the folder that
/// holds OpenPax.app.
/// </summary>
internal static class InstallFolder
{
    private const string MacProgramFolder = ".app/Contents/MacOS";

    public static string Of(string programFolder)
    {
        var folder = Path.TrimEndingDirectorySeparator(Path.GetFullPath(programFolder));
        if (folder.Replace('\\', '/').EndsWith(MacProgramFolder, StringComparison.Ordinal))
        {
            // MacOS, Contents, then OpenPax.app: its parent is the bundle's folder.
            var app = Path.GetDirectoryName(Path.GetDirectoryName(folder));
            return Path.GetDirectoryName(app) ?? folder;
        }

        return folder;
    }

    /// <summary>The program to start again after an update, in the install folder.</summary>
    public static string Program(string install)
    {
        if (OperatingSystem.IsWindows())
        {
            return Path.Combine(install, "openpax.exe");
        }

        var mac = Path.Combine(install, "OpenPax.app", "Contents", "MacOS", "openpax");
        return File.Exists(mac) ? mac : Path.Combine(install, "openpax");
    }

    /// <summary>Whether this user may change the install folder (an install in Program Files, say, needs more).</summary>
    public static bool Writable(string install)
    {
        var probe = Path.Combine(install, $".openpax-write-{Environment.ProcessId}");
        try
        {
            File.WriteAllText(probe, "");
            File.Delete(probe);
            return true;
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            return false;
        }
    }
}
