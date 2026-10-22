namespace OpenPax.Data.Views;

/// <summary>Class 8033 (read). Track picture + 50 when disabled.</summary>
/// <param name="Unknown">Always 1100; the scroll bar's own code never reads it.</param>
/// <param name="IncreasePicture">Right or bottom arrow.</param>
/// <param name="DecreasePicture">Left or top arrow.</param>
/// <param name="Target">Id of the widget it scrolls, looked up in the same parent.</param>
public sealed record ScrollBarDetails(int Unknown, int ThumbPicture, int TrackPicture, int IncreasePicture, int DecreasePicture, byte Vertical, int Target) : WidgetDetails;
