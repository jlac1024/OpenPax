namespace OpenPax.TestSupport;

/// <summary>
/// Finds the game data build.sh imports into build/data, for tests that
/// draw screens. They skip with a clear message when it is not there.
/// </summary>
internal static class ImportedData
{
    public static string RequireFolder()
    {
        var folder = Find();
        Assert.SkipWhen(folder is null, "Imported data not found: run build.sh with OPENPAX_GAME (or build/local.env) set to your GOG copy of the game");

        // SkipWhen throws when folder is null, so it is set here.
        return folder!;
    }

    private static string? Find()
    {
        for (var dir = new DirectoryInfo(AppContext.BaseDirectory); dir is not null; dir = dir.Parent)
        {
            if (File.Exists(Path.Combine(dir.FullName, "OpenPax.slnx")))
            {
                var data = Path.Combine(dir.FullName, "build", "data");
                return File.Exists(Path.Combine(data, "import.toml")) ? data : null;
            }
        }

        return null;
    }
}
