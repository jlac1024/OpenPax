namespace OpenPax.Data.Views;

/// <summary>
/// Class 8017, an editable text field (read). Typing stops
/// when the length equals <see cref="MaxLength"/> (the original tests for
/// "not equal", not "less than").
/// </summary>
public sealed record TextEditDetails(int MaxLength, byte Editable, byte Font, byte Color, byte ShadowColor, byte Align, byte Style) : WidgetDetails;
