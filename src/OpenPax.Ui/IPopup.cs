namespace OpenPax.Ui;

/// <summary>
/// A popup window over the screen (app +0x70): it takes the mouse and keys
/// while open; the screen's keys are refused and the game clock runs on.
/// </summary>
public interface IPopup
{
    ViewWindow Window { get; }

    /// <summary>Handles what the popup's widgets said; true once it should close.</summary>
    bool Handle(UiMessage message);

    /// <summary>A key; true once it should close.</summary>
    bool Key(UiKey key);
}
