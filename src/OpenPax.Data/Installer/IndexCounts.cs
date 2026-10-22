namespace OpenPax.Data.Installer;

/// <summary>How many of each kind of entry an Inno Setup index holds, those the import reads past or reads.</summary>
internal readonly record struct IndexCounts(
    uint Languages,
    uint Messages,
    uint Permissions,
    uint Types,
    uint Components,
    uint Tasks,
    uint Directories,
    uint Files,
    uint Data);
