namespace OpenPax.Data.Views;

/// <summary>
/// One column heading of a header bar (29 bytes, read).
/// The file holds a width and a height; the reader turns them into a
/// right and a bottom edge.
/// </summary>
/// <param name="Code">Command sent when clicked, for example 502; probably a sort key.</param>
/// <param name="Flag">1: clicking it chooses it.</param>
/// <param name="SelectedColor">Text color when selected; the cell is then filled with <see cref="Color"/>.</param>
public sealed record HeaderCell(TextRef Text, byte Align, int Left, int Top, int Width, int Height, byte Flag, int Code, byte Font, byte Style, byte SelectedColor, byte Color);
