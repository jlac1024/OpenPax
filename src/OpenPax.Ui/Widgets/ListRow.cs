using OpenPax.Render;

namespace OpenPax.Ui.Widgets;

/// <summary>One row of a list (the list item base): its height, how it draws, and how it sorts.</summary>
public abstract class ListRow
{
    public virtual int Height => 14;

    /// <summary>The row's width in a list laid out across (a horizontal list).</summary>
    public virtual int Width => 0;

    /// <summary>
    /// How much wider than the original layout the list is drawn (docs/ui/widescreen.md),
    /// set by the list before each draw. A row keeps its columns right of the
    /// first in place from the right by moving them this much, so the room
    /// goes to its first, name column.
    /// </summary>
    public int Grown { get; internal set; }

    /// <summary>Draws the row with its top left at (x, y).</summary>
    public abstract void Draw(Framebuffer target, UiAssets assets, int x, int y, int width, bool selected);

    /// <summary>Compares two rows by a header's sort code; equal rows keep their order.</summary>
    public abstract int Compare(ListRow other, int sortCode);
}
