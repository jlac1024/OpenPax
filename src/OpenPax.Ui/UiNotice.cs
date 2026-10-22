namespace OpenPax.Ui;

/// <summary>
/// A notice in the message log (0x18 bytes, base): its text, how
/// urgent it is, and what a double click on it does. A transient one (game
/// speed, pause) shows until the next message and is never kept.
/// </summary>
/// <param name="Priority">0 normal, 1 notable, 2 urgent.</param>
/// <param name="Action">The double click (vtable slot 0); null does nothing.</param>
public sealed record UiNotice(string Text, int Priority, bool Transient, Action<UiApp>? Action);
