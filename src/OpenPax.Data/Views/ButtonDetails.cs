namespace OpenPax.Data.Views;

/// <summary>
/// Class 8002 button (read), and 8039 animated button (read at
/// ), which adds <see cref="AnimFirstPicture"/> and
/// <see cref="AnimFrames"/>. Clicking sends the button's own id.
/// </summary>
/// <param name="Latching">1: stays down when clicked.</param>
/// <param name="Picture">0 draws a bevel instead.</param>
/// <param name="TextX">Text offset.</param>
/// <param name="PressX">Text shift while pressed; for 8039 the animation overlay offset.</param>
public sealed record ButtonDetails(
    byte Latching,
    int Picture,
    TextRef Text,
    byte Color,
    byte PressedColor,
    byte DisabledColor,
    byte ShadowColor,
    byte Align,
    byte Style,
    byte Font,
    int TextX,
    int TextY,
    int PressX,
    int PressY,
    int Sound,
    int? AnimFirstPicture,
    int? AnimFrames) : WidgetDetails;
