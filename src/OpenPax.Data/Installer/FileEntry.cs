namespace OpenPax.Data.Installer;

/// <summary>
/// A file the installer puts in place: where (with Inno Setup's folder
/// constants such as {app}), which packed file it comes from, and the
/// script check, if any, that decides whether it is installed.
/// </summary>
internal sealed record FileEntry(string Destination, uint Location, string Check, bool IsUserFile);
