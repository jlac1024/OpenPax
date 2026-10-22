namespace OpenPax.Data.Installer;

/// <summary>
/// Where an Inno Setup installer keeps its parts (docs/formats/inno-setup.md):
/// the setup index and the packed files, as offsets into the .exe.
/// </summary>
internal readonly record struct LoaderTable(long SetupData, long FileData);
