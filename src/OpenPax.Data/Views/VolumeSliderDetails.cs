namespace OpenPax.Data.Views;

/// <summary>
/// Class 8041 (read). On load the original replaces the
/// stored value with the current volume setting, clamped to the range.
/// </summary>
/// <param name="Channel">1 sound effects, 2 music, 3 voice.</param>
public sealed record VolumeSliderDetails(ValueRange Range, int Channel) : WidgetDetails;
