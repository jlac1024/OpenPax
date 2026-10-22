using OpenPax.Forms.Layout;

namespace OpenPax.Forms.Controls;

/// <summary>
/// Rows and columns of fixed, Auto or star sizes; children placed by
/// Grid.Row and Grid.Column, and may span. Stars share what fixed and
/// Auto sizes leave, by their weights.
/// </summary>
public sealed class Grid : Panel
{
    private int[] _rows = [];
    private int[] _columns = [];

    public IReadOnlyList<GridLength> RowDefinitions { get; set; } = [];

    public IReadOnlyList<GridLength> ColumnDefinitions { get; set; } = [];

    /// <summary>Space between rows and between columns.</summary>
    public int RowSpacing { get; set; }

    public int ColumnSpacing { get; set; }

    protected override Size MeasureOverride(Size available)
    {
        var rows = Defs(RowDefinitions);
        var cols = Defs(ColumnDefinitions);
        foreach (var child in Shown)
        {
            child.Measure(new Size(Size.Infinite, Size.Infinite));
        }

        _columns = Sizes(cols, available.Width, ColumnSpacing, c => c.GridColumn, c => c.GridColumnSpan, c => c.DesiredSize.Width);
        foreach (var child in Shown)
        {
            // Measured again across its columns, so wrapped text knows its width.
            child.Measure(new Size(Span(_columns, child.GridColumn, child.GridColumnSpan, ColumnSpacing), Size.Infinite));
        }

        _rows = Sizes(rows, available.Height, RowSpacing, c => c.GridRow, c => c.GridRowSpan, c => c.DesiredSize.Height);
        return new Size(Total(_columns, ColumnSpacing), Total(_rows, RowSpacing));
    }

    protected override void ArrangeOverride(Rect bounds)
    {
        var rows = Defs(RowDefinitions);
        var cols = Defs(ColumnDefinitions);
        _columns = Sizes(cols, bounds.Width, ColumnSpacing, c => c.GridColumn, c => c.GridColumnSpan, c => c.DesiredSize.Width);
        _rows = Sizes(rows, bounds.Height, RowSpacing, c => c.GridRow, c => c.GridRowSpan, c => c.DesiredSize.Height);
        foreach (var child in Shown)
        {
            var c = Math.Clamp(child.GridColumn, 0, _columns.Length - 1);
            var r = Math.Clamp(child.GridRow, 0, _rows.Length - 1);
            var x = bounds.X + Offset(_columns, c, ColumnSpacing);
            var y = bounds.Y + Offset(_rows, r, RowSpacing);
            child.Arrange(new Rect(x, y, Span(_columns, c, child.GridColumnSpan, ColumnSpacing), Span(_rows, r, child.GridRowSpan, RowSpacing)));
        }
    }

    private static IReadOnlyList<GridLength> Defs(IReadOnlyList<GridLength> defs) => defs.Count == 0 ? [GridLength.Star] : defs;

    /// <summary>Each track's size: fixed as given, Auto to its widest single-span child, stars sharing the rest.</summary>
    private int[] Sizes(IReadOnlyList<GridLength> defs, int room, int spacing, Func<Element, int> index, Func<Element, int> span, Func<Element, int> need)
    {
        var sizes = new int[defs.Count];
        for (var i = 0; i < defs.Count; i++)
        {
            sizes[i] = defs[i].Unit switch
            {
                GridUnit.Pixel => defs[i].Value,
                GridUnit.Auto => Shown.Where(c => Math.Clamp(index(c), 0, defs.Count - 1) == i && span(c) == 1).Select(need).DefaultIfEmpty(0).Max(),
                _ => 0,
            };
        }

        var stars = defs.Where(d => d.Unit == GridUnit.Star).Sum(d => d.Value);
        if (stars > 0)
        {
            var left = room >= Size.Infinite ? 0 : Math.Max(0, room - sizes.Sum() - spacing * Math.Max(0, defs.Count - 1));
            var given = 0;
            var last = -1;
            for (var i = 0; i < defs.Count; i++)
            {
                if (defs[i].Unit == GridUnit.Star)
                {
                    sizes[i] = room >= Size.Infinite
                        ? Shown.Where(c => Math.Clamp(index(c), 0, defs.Count - 1) == i && span(c) == 1).Select(need).DefaultIfEmpty(0).Max()
                        : left * defs[i].Value / stars;
                    given += sizes[i];
                    last = i;
                }
            }

            // Rounding's leftover goes to the last star.
            if (room < Size.Infinite && last >= 0)
            {
                sizes[last] += left - given;
            }
        }

        return sizes;
    }

    private static int Offset(int[] sizes, int index, int spacing) => sizes.Take(index).Sum() + index * spacing;

    private static int Span(int[] sizes, int index, int span, int spacing)
    {
        var end = Math.Min(sizes.Length, index + Math.Max(1, span));
        return sizes[index..end].Sum() + (end - index - 1) * spacing;
    }

    private static int Total(int[] sizes, int spacing) => sizes.Sum() + Math.Max(0, sizes.Length - 1) * spacing;
}
