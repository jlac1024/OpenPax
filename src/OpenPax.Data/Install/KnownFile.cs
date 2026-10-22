namespace OpenPax.Data.Install;

/// <summary>A file of the GOG build: path relative to the game folder, size, sha256 in lowercase hex.</summary>
internal readonly record struct KnownFile(string Path, long Size, string Sha256);
