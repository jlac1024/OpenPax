namespace OpenPax.Ui;

/// <summary>
/// What a widget tells its window, as the original broadcasts:
/// a message number (usually the widget's own id) and a value.
/// </summary>
public readonly record struct UiMessage(int Message, int Value)
{
    /// <summary>The pointer entered or is over a widget; the value is its id.</summary>
    public const int Within = 3001;

    /// <summary>The pointer left a widget.</summary>
    public const int Left = 3002;

    /// <summary>A hot spot was pressed, or the pointer came back onto it while held.</summary>
    public const int HotSpotPressed = 3007;

    /// <summary>A hot spot was released, or the pointer left it while held.</summary>
    public const int HotSpotReleased = 3008;

    /// <summary>A text edit's text changed; the value is its id (0 after deleting).</summary>
    public const int TextChanged = 1106;

    /// <summary>Return or Tab in a text edit; the value is its id.</summary>
    public const int TextDone = 1107;
}
