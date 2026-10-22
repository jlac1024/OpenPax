namespace OpenPax.Ui;

/// <summary>
/// Editing keys as the game's key codes number them (1000 and up); the
/// host maps real keys onto these. Return and Tab (1011, 1012) both end
/// editing; which real keys the original mapped there is open.
/// </summary>
public enum UiKey
{
    Left = 1000,
    Right = 1001,
    Up = 1002,
    Down = 1003,
    Home = 1005,
    End = 1006,
    Backspace = 1009,
    Delete = 1010,
    Return = 1011,
    Tab = 1012,
    Escape = 1013,
}
