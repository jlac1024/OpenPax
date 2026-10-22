namespace OpenPax.Modding;

/// <summary>A mod found in the mods folder: its manifest and where it is.</summary>
public sealed record ModInfo(ModManifest Manifest, string Folder)
{
    /// <summary>SHA-256 of its files (ModPackage.HashOf), worked out when it is first asked for.</summary>
    public string Hash => field ??= ModPackage.HashOf(Folder);

    /// <summary>The mod as a mod set names it.</summary>
    public ModKey Key => ModKey.Of(this);

    // Equal by manifest and folder; the hash worked out or not makes no difference.
    public bool Equals(ModInfo? other) => other is not null && Manifest == other.Manifest && Folder == other.Folder;

    public override int GetHashCode() => HashCode.Combine(Manifest, Folder);

    /// <summary>Its data pack's folder, or null when it brings none.</summary>
    public string? DataFolder => Directory.Exists(Path.Combine(Folder, ModManifest.DataFolder)) ? Path.Combine(Folder, ModManifest.DataFolder) : null;

    /// <summary>Its assembly's path, or null when it brings none.</summary>
    public string? AssemblyPath => Manifest.Assembly is { } a ? Path.Combine(Folder, a) : null;
}
