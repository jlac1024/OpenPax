using OpenPax.Sim;

namespace OpenPax.Ui;

/// <summary>
/// The player's settings (the original's preferences object, app +0x60,
/// kept in Data\PrefData): named whole numbers, flags being 0 or 1. A name
/// asked for and missing gets its default. The host loads and saves them
/// as the config file; screens read and write them here.
/// </summary>
// get flag, get int, set  /
public sealed class Preferences
{
    public const string PlaySurfaceAnim = "PlaySurfaceAnim";
    public const string PlayMovies = "PlayMovies";
    public const string AutoSaveGame = "AutoSaveGame";
    public const string GameSpeed = "GameSpeed";
    public const string CombatKeyScroll = "CombatKeyScroll";
    public const string CombatScroll = "CombatScroll";
    public const string SoundFxVolume = "SoundFXVolume";
    public const string RedbookVolume = "RedbookVolume";
    public const string VoiceVolume = "VoiceVolume";

    /// <summary>The port's own: the original screens drawn at 2x with the upscaled art (1), or at their own size (0).</summary>
    public const string UpscaledArt = "UpscaledArt";

    /// <summary>The port's own: text in the game's fonts (0), those fonts upscaled (1), or the modern font (2).</summary>
    public const string TextFont = "TextFont";

    /// <summary>The port's own: text plain (0), outlined in black keeping its colors (1), or outlined white (2).</summary>
    public const string TextOutline = "TextOutline";

    /// <summary>The port's own: the window covers the display at its own resolution (1) or is a window (0); Alt+Enter switches.</summary>
    public const string Fullscreen = "Fullscreen";

    /// <summary>The port's own: the starfield behind the port's screens flies by slowly (1) or stands still (0).</summary>
    public const string MovingStars = "MovingStars";

    /// <summary>The port's own: how fast the moving starfield flies, 0 (still) to 100 (twice the default 50).</summary>
    public const string StarSpeed = "StarSpeed";

    /// <summary>The port's own: the system view shows a sky made for each system (1) or the game's seven nebula pictures (0).</summary>
    public const string MadeSkies = "MadeSkies";

    /// <summary>The port's own: frames wait for the display's refresh (1), or are paced by the clock alone (0).</summary>
    public const string VSync = "VSync";

    /// <summary>The port's own gameplay toggle: speed 0 pauses the game (1) instead of the original's slowest speed (0). On at Jeff's call (decisions.md).</summary>
    public const string ZeroPauses = "SpeedZeroPauses";

    /// <summary>The port's own fix settings (docs/sim/fix-candidates.md), each 0 (as the original, the default) or 1; a new game takes them.</summary>
    public static IReadOnlyDictionary<Fixes, string> FixNames { get; } = Enum.GetValues<Fixes>().Where(f => f != Fixes.None).ToDictionary(f => f, f => "Fix" + f);

    /// <summary>The fixes a new game plays with.</summary>
    public Fixes ChosenFixes() => FixNames.Where(kv => Flag(kv.Value)).Aggregate(Fixes.None, (all, kv) => all | kv.Key);

    /// <summary>The flag (0 to 29) kept in the original's PrefData; the port starts Game Setup and a lobby room on it (Options).</summary>
    public const string FlagId = "FlagID";

    /// <summary>The fleet (0 to 15), whose ships each have their own look, kept in PrefData as the flag is.</summary>
    public const string FleetId = "FleetID";

    /// <summary>The port's own: the stock species (index in the Species Selection list) a lobby room starts on.</summary>
    public const string PreferredSpecies = "PreferredSpecies";

    /// <summary>
    /// The defaults the original's code writes when a name is missing; the
    /// volumes take the sliders' layout value 8, stored as 8 x 12.
    /// </summary>
    public static IReadOnlyDictionary<string, int> Defaults { get; } = new Dictionary<string, int>
    {
        [PlaySurfaceAnim] = 1,
        [PlayMovies] = 1,
        [AutoSaveGame] = 1,
        [GameSpeed] = 3,
        [CombatKeyScroll] = 15,
        [CombatScroll] = 15,
        [SoundFxVolume] = 96,
        [RedbookVolume] = 96,
        [VoiceVolume] = 96,
        [UpscaledArt] = 1,
        [TextFont] = 2,
        [TextOutline] = 1,
        [Fullscreen] = 0,
        [MovingStars] = 1,
        [StarSpeed] = 50,
        [MadeSkies] = 1,
        [VSync] = 1,
        [ZeroPauses] = 1,
    };

    private readonly Dictionary<string, int> _values = [];

    /// <summary>Raised with the name after a value changes.</summary>
    public event Action<string>? Changed;

    public IReadOnlyDictionary<string, int> Values => _values;

    public int Get(string name)
    {
        if (!_values.TryGetValue(name, out var value))
        {
            value = Defaults.GetValueOrDefault(name);
            _values[name] = value;
        }

        return value;
    }

    public bool Flag(string name) => Get(name) != 0;

    /// <summary>Stores a value; with notify, those listening hear of it if it changed.</summary>
    public void Set(string name, int value, bool notify = true)
    {
        var changed = !_values.TryGetValue(name, out var old) || old != value;
        _values[name] = value;
        if (notify && changed)
        {
            Changed?.Invoke(name);
        }
    }

    /// <summary>Takes values read from the config file, without telling anyone.</summary>
    public void Load(IEnumerable<KeyValuePair<string, int>> values)
    {
        foreach (var (name, value) in values)
        {
            _values[name] = value;
        }
    }
}
