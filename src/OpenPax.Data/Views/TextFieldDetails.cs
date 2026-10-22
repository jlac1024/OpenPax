namespace OpenPax.Data.Views;

/// <summary>
/// Text set by code at runtime: class 8028, one line (read,
/// int32 fields), and class 8024, word-wrapped (read, byte
/// fields). Both store style, align, font, color, shadow color.
/// </summary>
public sealed record TextFieldDetails(TextStyle Style) : WidgetDetails;
