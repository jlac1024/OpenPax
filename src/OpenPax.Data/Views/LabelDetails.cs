namespace OpenPax.Data.Views;

/// <summary>
/// Class 8025, fixed text (read). All seven fields are int32
/// in the file although they hold byte-sized values.
/// </summary>
public sealed record LabelDetails(TextRef Text, TextStyle Style) : WidgetDetails;
