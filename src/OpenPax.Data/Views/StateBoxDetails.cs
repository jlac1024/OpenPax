namespace OpenPax.Data.Views;

/// <summary>
/// Class 8035, a toggle with several states (read). State 0
/// shows the picture, state 1 picture + 200, states 2 and 3 picture + 100.
/// Text always uses font 1.
/// </summary>
public sealed record StateBoxDetails(TextRef Text, byte Style, byte Color, byte DisabledColor, byte Align, int Picture, int Sound) : WidgetDetails;
