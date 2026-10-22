namespace OpenPax.TestSupport;

/// <summary>
/// Finds the GOG install in references/gog/ for tests that need real game
/// data. Those tests skip with a clear message when it is not there.
/// </summary>
internal static class GameData
{
    public const string FolderName = "Pax Imperia - Eminent Domain";

    public static string RequireFolder()
    {
        var folder = Find();
        Assert.SkipWhen(folder is null, $"Game data not found: put the GOG install in references/gog/{FolderName}/");
        // SkipWhen throws when folder is null, so it is set here.
        return folder!;
    }

    private static string? Find()
    {
        for (var dir = new DirectoryInfo(AppContext.BaseDirectory); dir is not null; dir = dir.Parent)
        {
            if (File.Exists(Path.Combine(dir.FullName, "OpenPax.slnx")))
            {
                var game = Path.Combine(dir.FullName, "references", "gog", FolderName);
                return Directory.Exists(game) ? game : null;
            }
        }

        return null;
    }
}
