namespace OpenPax.Ui;

/// <summary>
/// The app's keyboard commands (the "PaxIIAccelerators" table, handled by
/// ), numbered as the original's command ids. The host maps keys
/// onto them.
/// </summary>
public enum UiCommand
{
    Bridge = 300,
    SectorDisplay = 301,
    Research = 302,
    FleetCommand = 303,
    DomesticPolicy = 304,
    ForeignAffairs = 305,
    PlanetarySurface = 306,
    ShipDesign = 307,
    GameOptions = 308,
    Mode20 = 310,
    SpeedUp = 1200,
    SlowDown = 1201,
    Pause = 1202,

    /// <summary>Ctrl F1: save as "QuickSave".</summary>
    QuickSave = 1102,

    /// <summary>Ctrl F2: load "QuickSave".</summary>
    QuickLoad = 1103,

    /// <summary>Ctrl F3: load "PaxAutoSaveGame".</summary>
    LoadAutoSave = 1104,

    /// <summary>Alt X: quit, with the credits and an autosave.</summary>
    Quit = 10,

    /// <summary>Alt N: back to the start screen, dropping the game.</summary>
    NewGame = 2,

    /// <summary>The port's own developer console (`); not in the original.</summary>
    Console = 2000,
}
