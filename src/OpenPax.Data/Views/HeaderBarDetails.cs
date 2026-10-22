namespace OpenPax.Data.Views;

/// <summary>Class 8034, clickable column headings for a list (read).</summary>
public sealed record HeaderBarDetails(int Target, IReadOnlyList<HeaderCell> Cells) : WidgetDetails;
