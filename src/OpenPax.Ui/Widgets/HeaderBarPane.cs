using OpenPax.Data.Views;
using OpenPax.Render;

namespace OpenPax.Ui.Widgets;

/// <summary>
/// Class 8034, column headings for a list. Clicking a clickable heading not
/// already chosen chooses it and sorts its list by the heading's code.
/// </summary>
public sealed class HeaderBarPane(ViewNode node, int left, int top, HeaderBarDetails details) : Pane(node, left, top)
{
    /// <summary>Sent with a heading's code when it is chosen (1104).</summary>
    public const int SortChosen = 1104;

    public HeaderBarDetails Details { get; } = details;

    /// <summary>The chosen heading, or -1.</summary>
    public int Selected { get; private set; } = -1;

    /// <summary>Chooses a heading, sorts the list and says so.</summary>
    public void Select(int index)
    {
        Selected = index;
        if (index >= 0 && index < Details.Cells.Count)
        {
            var code = Details.Cells[index].Code;
            if (Window?.Get(Details.Target) is ListBoxPane list)
            {
                list.SortBy(code);
            }

            Post(SortChosen, code);
        }
    }

    /// <summary>The port's own: the code of the heading under a point, clickable or not, or null.</summary>
    public int? CodeAt(int x, int y)
    {
        foreach (var c in Details.Cells)
        {
            var (cellLeft, cellWidth) = Cell(c.Left, c.Width);
            if (x >= cellLeft && x <= cellLeft + cellWidth && y >= c.Top && y <= c.Top + c.Height)
            {
                return c.Code;
            }
        }

        return null;
    }

    /// <summary>The port's own: the code of the heading on a row (any point across), or null.</summary>
    public int? CodeOnRow(int y) => Details.Cells.FirstOrDefault(c => y >= c.Top && y <= c.Top + c.Height)?.Code;

    protected internal override void MouseDown(int x, int y, int clicks)
    {
        for (var i = 0; i < Details.Cells.Count; i++)
        {
            var c = Details.Cells[i];
            var (cellLeft, cellWidth) = Cell(c.Left, c.Width);
            if (c.Flag != 0 && i != Selected && x >= cellLeft && x <= cellLeft + cellWidth && y >= c.Top && y <= c.Top + c.Height)
            {
                Select(i);
                return;
            }
        }
    }

    /// <summary>A heading cell where a wide window has put it (docs/ui/widescreen.md); as the layout has it at 640.</summary>
    private (int Left, int Width) Cell(int left, int width) => Window?.Map(left, width) ?? (left, width);

    protected override void Draw(Framebuffer target)
    {
        for (var i = 0; i < Details.Cells.Count; i++)
        {
            var c = Details.Cells[i];
            var (cellLeft, cellWidth) = Cell(c.Left, c.Width);
            var chosen = i == Selected;
            if (chosen)
            {
                target.FillRect(cellLeft, c.Top, cellWidth, c.Height, c.Color);
            }

            var font = Assets.Fonts.ForIndex(c.Font);
            var x = c.Align switch
            {
                1 => (cellLeft + cellLeft + cellWidth) / 2,
                2 => cellLeft + cellWidth - 2,
                _ => cellLeft + 2,
            };
            var baseline = TextPlacement.Baseline(font, c.Style, c.Top + c.Height);
            font.Draw(target, Assets.Strings.Get(c.Text), x, baseline, c.Style, c.Align, chosen ? c.SelectedColor : c.Color, 0);
        }
    }
}
