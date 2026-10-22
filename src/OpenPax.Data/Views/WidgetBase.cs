namespace OpenPax.Data.Views;

/// <summary>
/// Fields every window element has (seven int32, read).
/// Position is relative to the window, also for widgets inside a group; width and height are a size, not
/// right and bottom.
/// </summary>
/// <param name="Visible">0 hidden, 2 shown. A 1 in the data is turned into 2 on load, as the original does.</param>
/// <param name="Enabled">0 disabled, 1 enabled, 2 enabled but held by the parent.</param>
public sealed record WidgetBase(int Id, int Left, int Top, int Width, int Height, int Visible, int Enabled);
