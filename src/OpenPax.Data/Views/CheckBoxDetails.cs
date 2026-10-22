namespace OpenPax.Data.Views;

/// <summary>Class 8016 (read). The label is drawn right of the picture; picture + 100 when checked.</summary>
public sealed record CheckBoxDetails(TextRef Text, byte Style, byte Font, byte Color, byte CheckedColor, byte Align, int Picture, int Sound) : WidgetDetails;
