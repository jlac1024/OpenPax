namespace OpenPax.Data.Views;

/// <summary>Class 8021, an invisible click area (read).</summary>
/// <param name="Cursor">Cursor shown on hover, for example 250.</param>
public sealed record HotSpotDetails(int Sound, int Cursor) : WidgetDetails;
