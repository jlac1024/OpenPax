using OpenPax.Forms.Drawing;
using OpenPax.Forms.Input;
using OpenPax.Forms.Layout;

namespace OpenPax.Forms.Controls;

/// <summary>Items in a column in the game's black list well, the picked one on a green bar; a scroll bar when they do not fit. A double click or Return fires ItemActivated.</summary>
public class ListBox : ItemsControl
{
    private readonly ScrollBar _bar;

    public ListBox()
    {
        _bar = Adopt(new ScrollBar());
    }

    public int? Font { get; set; }

    /// <summary>Keeps the last item in view as items are added, as a chat log does.</summary>
    public bool FollowLast { get; set; }

    public override bool Focusable => true;

    public override IEnumerable<Element> Children => _bar.Visibility == Visibility.Visible ? [_bar] : [];

    public event EventHandler? ItemActivated;

    protected int FontIndex => Font ?? Theme.DefaultFont;

    protected int RowHeight => Theme.LineHeight(FontIndex) + 2;

    private Rect Inner => Bounds.Deflate(new Thickness(3, 3, _bar.Visibility == Visibility.Visible ? ScrollBar.Thickness + 1 : 3, 3));

    protected override Size MeasureOverride(Size available)
    {
        var font = Theme.Fonts(FontIndex);
        var widest = Items.Count == 0 ? 40 : Items.Max(i => Theme.Measure(FontIndex, TextOf(i)));
        return new Size(widest + 10 + ScrollBar.Thickness, Math.Max(3, Items.Count) * RowHeight + 6);
    }

    protected override void ArrangeOverride(Rect bounds)
    {
        var rows = Math.Max(1, (bounds.Height - 6) / RowHeight);
        _bar.Maximum = Math.Max(0, Items.Count - rows) * RowHeight;
        _bar.Viewport = rows * RowHeight;
        _bar.SmallStep = RowHeight;
        _bar.Value = FollowLast ? _bar.Maximum : _bar.Value;
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
        for (var i = 0; i < Items.Count; i++, y += RowHeight)
        {
            if (y + RowHeight < inner.Y || y > inner.Bottom)
            {
                continue;
            }

            if (i == SelectedIndex)
            {
                dc.Fill(new Rect(inner.X, y, inner.Width, RowHeight), Theme.Selection);
            }

            dc.Text(TextOf(Items[i]), inner.X + 4, y + 1, i == SelectedIndex ? Theme.Text : Theme.DimText, FontIndex);
        }

        dc.PopClip();
        if (IsFocused)
        {
            dc.Outline(Bounds, Theme.Accent.WithAlpha(120));
        }
    }

    /// <summary>The item at a point, or -1.</summary>
    public int IndexAt(int x, int y)
    {
        var inner = Inner;
        if (!inner.Contains(x, y))
        {
            return -1;
        }

        var i = (y - inner.Y + _bar.Value) / RowHeight;
        return i < Items.Count ? i : -1;
    }

    protected internal override void OnMouseDown(int x, int y, bool right)
    {
        if (IndexAt(x, y) is >= 0 and var i)
        {
            SelectedIndex = i;
        }
    }

    protected internal override void OnDoubleClick(int x, int y)
    {
        if (IndexAt(x, y) is >= 0 and var i)
        {
            SelectedIndex = i;
            ItemActivated?.Invoke(this, EventArgs.Empty);
        }
    }

    protected internal override bool OnWheel(int delta) => _bar.OnWheel(delta);

    protected internal override bool OnKey(FormKey key, bool shift)
    {
        var rows = Math.Max(1, _bar.Viewport / Math.Max(1, RowHeight));
        var next = key switch
        {
            FormKey.Up => SelectedIndex - 1,
            FormKey.Down => SelectedIndex + 1,
            FormKey.PageUp => SelectedIndex - rows,
            FormKey.PageDown => SelectedIndex + rows,
            FormKey.Home => 0,
            FormKey.End => Items.Count - 1,
            _ => int.MinValue,
        };
        if (key == FormKey.Return && SelectedIndex >= 0)
        {
            ItemActivated?.Invoke(this, EventArgs.Empty);
            return true;
        }

        if (next == int.MinValue || Items.Count == 0)
        {
            return false;
        }

        SelectedIndex = Math.Clamp(next, 0, Items.Count - 1);
        var top = SelectedIndex * RowHeight;
        if (top < _bar.Value)
        {
            _bar.Value = top;
        }
        else if (top + RowHeight > _bar.Value + _bar.Viewport)
        {
            _bar.Value = top + RowHeight - _bar.Viewport;
        }

        return true;
    }
}
