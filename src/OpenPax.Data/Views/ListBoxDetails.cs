namespace OpenPax.Data.Views;

/// <summary>Class 8019, a scrolling list (read).</summary>
/// <param name="SelectMessage">Sent on selection; usually the list's own id.</param>
/// <param name="ActivateMessage">Sent on mouse-up.</param>
/// <param name="Flag89">Affects how scrolling snaps; exact meaning open.</param>
public sealed record ListBoxDetails(int SelectMessage, int ActivateMessage, byte Vertical, byte Flag89) : WidgetDetails;
