using OpenPax.Data.Views;
using OpenPax.Render;

namespace OpenPax.Ui.Widgets;

/// <summary>
/// Class 8019, a list of rows stacked from the top (or, with the VIEW's
/// vertical byte clear, from the left) and scrolled by a scroll bar.
/// Pressing selects the row under the pointer and follows it while held; a
/// double click sends the activate message at release.
/// </summary>
public sealed class ListBoxPane(ViewNode node, int left, int top, ListBoxDetails details) : Pane(node, left, top)
{
    private readonly List<ListRow> _rows = [];
    private bool _held;
    private bool _double;

    public ListBoxDetails Details { get; } = details;

    public IReadOnlyList<ListRow> Rows => _rows;

    public ListRow? Selected { get; private set; }

    /// <summary>Pixels scrolled from the top (+0x7C).</summary>
    public int Scroll { get; private set; }

    /// <summary>The header's sort code the rows are kept in, 0 for none.</summary>
    public int SortCode { get; private set; }

    /// <summary>Rows run left to right instead of top to bottom.</summary>
    public bool Horizontal => Details.Vertical == 0;

    /// <summary>The rows' length along the list, heights or widths.</summary>
    public int ContentHeight => _rows.Sum(Extent);

    private int View => Horizontal ? Width : Height;

    public bool Scrollable => ContentHeight > View;

    /// <summary>How far down the list is scrolled, 0 to 1.</summary>
    public double Fraction => Scrollable ? (double)Scroll / (ContentHeight - View) : 0.0;

    private int Extent(ListRow row) => Horizontal ? row.Width : row.Height;

    public void Clear()
    {
        _rows.Clear();
        Selected = null;
        Scroll = 0;
    }

    /// <summary>Puts a row before the first that sorts after it, else at the end.</summary>
    public void Add(ListRow row)
    {
        var index = _rows.FindIndex(r => r.Compare(row, SortCode) > 0);
        _rows.Insert(index < 0 ? _rows.Count : index, row);
        ClampScroll();
    }

    /// <summary>A header asked for another order: the rows are sorted again (the C library's sort).</summary>
    public void SortBy(int code)
    {
        SortCode = code;
        var rows = _rows.ToArray();
        Sim.MsvcSort.Sort(rows.AsSpan(), (a, b) => a.Compare(b, code));
        _rows.Clear();
        _rows.AddRange(rows);
    }

    /// <summary>Marks a row selected without telling anyone, as the fill does.</summary>
    public void Select(ListRow? row)
    {
        Selected = row;
    }

    /// <summary>Scrolls to a fraction; without free scrolling it snaps to whole rows.</summary>
    public double SetFraction(double fraction)
    {
        Scroll = (int)((ContentHeight - View) * fraction);
        if (Details.Flag89 == 0 && _rows.Count > 0)
        {
            var row = Extent(_rows[0]);
            Scroll = row * (int)((Scroll / (double)row) + 0.5);
        }

        ClampScroll();
        return Fraction;
    }

    /// <summary>One row down or up.</summary>
    public void Step(int direction)
    {
        Scroll += direction * (_rows.Count > 0 ? Extent(_rows[0]) : 14);
        ClampScroll();
    }

    private void ClampScroll()
    {
        Scroll = Math.Clamp(Scroll, 0, Math.Max(0, ContentHeight - View));
    }

    private ListRow? RowAt(int x, int y)
    {
        var along = Horizontal ? x : y;
        var start = (Horizontal ? Left : Top) - Scroll;
        foreach (var row in _rows)
        {
            if (along >= start && along < start + Extent(row))
            {
                return row;
            }

            start += Extent(row);
        }

        return null;
    }

    // The original selects on the held move, which its loop
    // calls right after a press; selecting here as well keeps a quick click working.
    protected internal override void MouseDown(int x, int y, int clicks)
    {
        if (Window?.RightButton ?? false)
        {
            return;
        }

        _held = true;
        _double = clicks >= 2;
        SelectAt(x, y);
    }

    protected internal override void MouseMove(int x, int y, long now)
    {
        if (_held && Inside(x, y))
        {
            SelectAt(x, y);
        }
    }

    protected internal override void MouseUp(int x, int y)
    {
        if (_held && _double && Inside(x, y) && RowAt(x, y) is { } row)
        {
            Post(Details.ActivateMessage, _rows.IndexOf(row));
        }

        _held = false;
        _double = false;
    }

    private void SelectAt(int x, int y)
    {
        if (RowAt(x, y) is { } row && row != Selected)
        {
            Selected = row;
            Post(Details.SelectMessage, _rows.IndexOf(row));
        }
    }

    /// <summary>The rows in view and where they are drawn, in the screen's coordinates.</summary>
    public IEnumerable<(ListRow Row, int X, int Y)> VisibleRows()
    {
        var start = (Horizontal ? Left : Top) - Scroll;
        var low = Horizontal ? Left : Top;
        foreach (var row in _rows)
        {
            if (start + Extent(row) > low && start < low + View)
            {
                yield return Horizontal ? (row, start, Top) : (row, Left, start);
            }

            start += Extent(row);
        }
    }

    protected override void Draw(Framebuffer target)
    {
        var start = (Horizontal ? Left : Top) - Scroll;
        var low = Horizontal ? Left : Top;
        foreach (var row in _rows)
        {
            if (start + Extent(row) > low && start < low + View)
            {
                var (x, y) = Horizontal ? (start, Top) : (Left, start);
                row.Grown = Original is { } o && !Horizontal ? Math.Max(0, Width - o.Width) : 0;
                row.Draw(target, Assets, x, y, Width, row == Selected);
            }

            start += Extent(row);
        }
    }
}
