namespace OpenPax.Data.Views;

/// <summary>Class 8027, a number with decrease and increase arrows (read).</summary>
/// <param name="Sound">Plays on increase; id + 100 on decrease.</param>
public sealed record SpinnerDetails(ValueRange Range, int DecreasePicture, int IncreasePicture, int BackgroundPicture, int Sound, byte Color) : WidgetDetails;
