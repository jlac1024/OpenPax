using OpenPax.Audio;
using OpenPax.Data;
using OpenPax.Modding;
using OpenPax.Ui;

namespace OpenPax.Host;

/// <summary>
/// Everything the game builds from a mod set: the mods, the data files with
/// their packs over the game's, the screens' assets, the sounds and the game
/// tables (docs/modding/multiplayer.md). Made again, whole, to load another
/// set in place; the old one's mods are then let go.
/// </summary>
internal sealed class ModRuntime
{
    private ModRuntime(ModHost mods, DataFiles files)
    {
        Mods = mods;
        Files = files;
        Assets = new UiAssets(files);
        Mixer = SoundFiles.Load(files);
        Launcher = new GameLauncher(files, mods);
    }

    public ModHost Mods { get; }

    public DataFiles Files { get; }

    public UiAssets Assets { get; }

    public Mixer Mixer { get; }

    public GameLauncher Launcher { get; }

    /// <summary>What went wrong loading the mods and reading their content, for the player to be told.</summary>
    public IEnumerable<string> Problems => Mods.Errors.Concat(Mods.Messages).Concat(Launcher.ContentErrors);

    /// <summary>The player's own set: the mods turned on, by id.</summary>
    public static ModRuntime Own(string dataFolder, IReadOnlyList<string> modsFolders, IReadOnlyList<string> enabled) =>
        Start(ModHost.Load(modsFolders, enabled), dataFolder);

    /// <summary>A room's set by its keys; the keys not found here come back in missing, and the set loads without them.</summary>
    public static ModRuntime ForKeys(string dataFolder, IReadOnlyList<string> modsFolders, IReadOnlyList<ModKey> keys, out IReadOnlyList<ModKey> missing) =>
        Start(ModHost.Load(modsFolders, keys, out missing), dataFolder);

    private static ModRuntime Start(ModHost mods, string dataFolder)
    {
        var runtime = new ModRuntime(mods, mods.Data(dataFolder));

        // With mods, their content is read now, so what it gets wrong is told at once.
        if (mods.Mods.Count > 0)
        {
            _ = runtime.Launcher.Tables;
        }

        return runtime;
    }
}
