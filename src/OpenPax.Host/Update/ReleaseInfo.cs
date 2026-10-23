namespace OpenPax.Host.Update;

/// <summary>
/// A release newer than the running game, and its bundle for this platform:
/// where to download it, its size, and the SHA-256 GitHub gives it.
/// </summary>
internal sealed record ReleaseInfo(
    ReleaseVersion Version,
    bool IsPreRelease,
    string Page,
    string AssetName,
    string AssetUrl,
    long Size,
    string Sha256);
