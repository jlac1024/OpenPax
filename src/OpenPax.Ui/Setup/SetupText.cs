namespace OpenPax.Ui.Setup;

/// <summary>What the first-run setup says (docs/first-run.md).</summary>
internal static class SetupText
{
    public const string Found = "Found your game. Press IMPORT, or pick another copy.";
    public const string NoneFound = "No copy of the game was found on this machine. Pick its folder, or GOG's offline installer.";
    public const string OnlyGalaxyStub = "Only GOG's small Galaxy installer was found, and it holds none of the game. In your GOG library, download the offline backup installer (setup_pax_imperia_....exe), then pick it here.";
    public const string Picked = "Press IMPORT to read the game from there.";
    public const string NotTheGame = "That is not the game's folder: pick the folder holding Pax Imperia.exe, or GOG's offline installer (setup_pax_imperia_....exe).";
    public const string GalaxyStub = "That is GOG's small Galaxy installer, which only downloads the game and holds none of it. In your GOG library, download the offline backup installer (setup_pax_imperia_....exe) and pick that.";
    public const string NotAnInstaller = "That is not GOG's offline installer for the game: pick setup_pax_imperia_....exe from your GOG library's offline backup downloads, or the installed game's folder.";
    public const string Starting = "Starting...";
    public const string Failed = "The import stopped: ";
}
