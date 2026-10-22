using OpenPax.Forms.Drawing;
using OpenPax.Forms.Input;
using OpenPax.Forms.Layout;

namespace OpenPax.Forms.Controls;

/// <summary>Nodes in a list well, indented by depth, each with children opened and closed by its arrow, Left and Right, or a double click.</summary>
public sealed class TreeView : Element
{
    private const int Indent = 10;
    private readonly ScrollBar _bar;
    private TreeViewItem? _selected;

    public TreeView()
    {
        _bar = Adopt(new ScrollBar());
    }

    public List<TreeViewItem> Items { get; } = [];

    public TreeViewItem? SelectedItem
    {
        get => _selected;
        set
        {
            if (Set(ref _selected, value))
            {
                SelectedItemChanged?.Invoke(this, EventArgs.Empty);
            }
        }
    }

    public int? Font { get; set; }

    public override bool Focusable => true;

    public override IEnumerable<Element> Children => _bar.Visibility == Visibility.Visible ? [_bar] : [];

    public event EventHandler? SelectedItemChanged;

    private int FontIndex => Font ?? Theme.DefaultFont;

    private int RowHeight => Theme.LineHeight(FontIndex) + 2;

    private Rect Inner => Bounds.Deflate(new Thickness(3, 3, _bar.Visibility == Visibility.Visible ? ScrollBar.Thickness + 1 : 3, 3));

    /// <summary>The nodes that show, with their depth, top to bottom.</summary>
    public IReadOnlyList<(TreeViewItem Item, int Depth)> Rows
    {
        get
        {
            var rows = new List<(TreeViewItem, int)>();
            void Add(IEnumerable<TreeViewItem> items, int depth)
            {
                foreach (var item in items)
                {
                    rows.Add((item, depth));
                    if (item.IsExpanded)
                    {
                        Add(item.Items, depth + 1);
                    }
                }
            }

            Add(Items, 0);
            return rows;
        }
    }

    protected override Size MeasureOverride(Size available)
    {
        var font = Theme.Fonts(FontIndex);
        var rows = Rows;
        var widest = rows.Count == 0 ? 40 : rows.Max(r => r.Depth * Indent + 10 + Theme.Measure(FontIndex, r.Item.Header));
        return new Size(widest + 10, Math.Max(3, rows.Count) * RowHeight + 6);
    }

    protected override void ArrangeOverride(Rect bounds)
    {
        var shown = Math.Max(1, (bounds.Height - 6) / RowHeight);
        _bar.Maximum = Math.Max(0, Rows.Count - shown) * RowHeight;
        _bar.Viewport = shown * RowHeight;
        _bar.SmallStep = RowHeight;
        _bar.Value = _bar.Value;
        _bar.Visibility = _bar.Maximum > 0 ? Visibility.Visible : Visibility.Collapsed;
        _bar.Measure(new Size(ScrollBar.Thickness, bounds.Height));
        _bar.Arrange(new Rect(bounds.Right - ScrollBar.Thickness, bounds.Y, ScrollBar.Thickness, bounds.Height));
    }

    protected override void OnRender(DrawContext dc)
    {
        dc.SkinOr(Theme.ListWell, Bounds, Theme.Well, true);
        var inner = Inner;
        dc.PushClip(inner);
        var y = inner.Y - _bar.Value;
        foreach (var (item, depth) in Rows)
        {
            var x = inner.X + 2 + depth * Indent;
            if (item == _selected)
            {
                dc.Fill(new Rect(inner.X, y, inner.Width, RowHeight), Theme.Selection);
            }

            if (item.Items.Count > 0)
            {
                Expander(dc, x, y + RowHeight / 2, item.IsExpanded);
            }

            dc.Text(item.Header, x + 9, y + 1, item == _selected ? Theme.Text : Theme.DimText, FontIndex);
            y += RowHeight;
        }

        dc.PopClip();
    }

    /// <summary>A small green triangle: pointing right when closed, down when open.</summary>
    private void Expander(DrawContext dc, int x, int centerY, bool open)
    {
        for (var i = 0; i < 4; i++)
        {
            var r = open ? new Rect(x + i, centerY - 2 + i, 7 - i * 2, 1) : new Rect(x + 1 + i, centerY - 3 + i, 1, 7 - i * 2);
            dc.Fill(r, Theme.Accent);
        }
    }

    private (TreeViewItem Item, int Depth)? RowAt(int y)
    {
        var inner = Inner;
        var i = (y - inner.Y + _bar.Value) / RowHeight;
        var rows = Rows;
        return y >= inner.Y && i >= 0 && i < rows.Count ? rows[i] : null;
    }

    protected internal override void OnMouseDown(int x, int y, bool right)
    {
        if (RowAt(y) is not { } row)
        {
            return;
        }

        var arrowX = Inner.X + 2 + row.Depth * Indent;
        if (x >= arrowX && x < arrowX + 9 && row.Item.Items.Count > 0)
        {
            row.Item.IsExpanded = !row.Item.IsExpanded;
        }

        SelectedItem = row.Item;
    }

    protected internal override void OnDoubleClick(int x, int y)
    {
        if (RowAt(y) is { } row)
        {
            row.Item.IsExpanded = !row.Item.IsExpanded;
            SelectedItem = row.Item;
        }
    }

    protected internal override bool OnWheel(int delta) => _bar.OnWheel(delta);

    protected internal override bool OnKey(FormKey key, bool shift)
    {
        var rows = Rows.Select(r => r.Item).ToList();
        var i = _selected is null ? -1 : rows.IndexOf(_selected);
        switch (key)
        {
            case FormKey.Up when i > 0:
                SelectedItem = rows[i - 1];
                return true;
            case FormKey.Down when i < rows.Count - 1:
                SelectedItem = rows[i + 1];
                return true;
            case FormKey.Right when _selected is { Items.Count: > 0 }:
                _selected.IsExpanded = true;
                return true;
            case FormKey.Left when _selected is not null:
                _selected.IsExpanded = false;
                return true;
            default:
                return false;
        }
    }
}
