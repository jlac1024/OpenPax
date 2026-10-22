namespace OpenPax.Data.Install;

/// <summary>What a picked .exe turned out to be (docs/first-run.md).</summary>
public enum InstallerKind
{
    /// <summary>Not a GOG installer we know: some other program, or not a program at all.</summary>
    Unknown,

    /// <summary>GOG's offline installer (Inno Setup), which holds the whole game.</summary>
    Offline,

    /// <summary>
    /// GOG's small Galaxy installer, which holds no game files: it downloads
    /// the game through GOG Galaxy, so there is nothing in it to import.
    /// </summary>
    GalaxyStub,
}
