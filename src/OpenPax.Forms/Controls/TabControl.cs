using OpenPax.Forms.Drawing;
using OpenPax.Forms.Layout;

namespace OpenPax.Forms.Controls;

/// <summary>Pages with a row of tabs in the game's small button art; only the picked page shows.</summary>
public sealed class TabControl : Element
{
    private const int TabHeight = 17;
    private readonly List<TabItem> _pages = [];
    private int _selected;

    public IReadOnlyList<TabItem> Pages => _pages;

    public int SelectedIndex
    {
        get => _selected;
        set
        {
            if (Set(ref _selected, Math.Clamp(value, 0, Math.Max(0, _pages.Count - 1))))
            {
                SelectionChanged?.Invoke(this, EventArgs.Empty);
            }
        }
    }

    public event EventHandler? SelectionChanged;

    /// <summary>Every page is in the tree, so names and bindings reach them; only the picked one draws and takes the mouse.</summary>
    public override IEnumerable<Element> Children => _pages;

    public void Add(TabItem page) => _pages.Add(Adopt(page));

    protected override Size MeasureOverride(Size available)
    {
        var inner = new Size(available.Width, Math.Max(0, available.Height - TabHeight - 8)).Deflate(new Thickness(4));
        var d = new Size(0, 0);
        foreach (var page in _pages)
        {
            var m = page.Measure(inner);
            d = new Size(Math.Max(d.Width, m.Width), Math.Max(d.Height, m.Height));
        }

        return new Size(Math.Max(d.Width + 8, TabsWidth()), d.Height + TabHeight + 8);
    }

    protected override void ArrangeOverride(Rect bounds)
    {
        var body = new Rect(bounds.X, bounds.Y + TabHeight, bounds.Width, Math.Max(0, bounds.Height - TabHeight)).Deflate(new Thickness(4));
        foreach (var page in _pages)
        {
            page.Arrange(body);
        }
    }

    protected override void OnRender(DrawContext dc)
    {
        var body = new Rect(Bounds.X, Bounds.Y + TabHeight, Bounds.Width, Math.Max(0, Bounds.Height - TabHeight));
        dc.Panel(body);
        dc.Bevel(body, false);
        var x = Bounds.X;
        for (var i = 0; i < _pages.Count; i++)
        {
            var w = TabWidth(i);
            var r = new Rect(x, Bounds.Y, w, TabHeight);
            dc.SkinOr(i == _selected ? Theme.TabPressed ?? Theme.Tab : Theme.Tab, r, Theme.Light, i == _selected);
            var text = _pages[i].Header;
            var h = dc.LineHeight(Theme.DefaultFont);
            dc.Text(text, x + (w - dc.Measure(text, Theme.DefaultFont, true)) / 2, Bounds.Y + (TabHeight - h) / 2, i == _selected ? Theme.ButtonPressedText : Theme.ButtonText, Theme.DefaultFont, true);
            x += w + 2;
        }
    }

    public override Element? HitTest(int x, int y)
    {
        if (!Bounds.Contains(x, y) || Visibility != Visibility.Visible)
        {
            return null;
        }

        return y >= Bounds.Y + TabHeight && _selected < _pages.Count ? _pages[_selected].HitTest(x, y) ?? this : this;
    }

    protected override void RenderChildren(DrawContext dc)
    {
        if (_selected < _pages.Count)
        {
            _pages[_selected].Render(dc);
        }
    }

    protected internal override void OnMouseDown(int x, int y, bool right)
    {
        var left = Bounds.X;
        for (var i = 0; i < _pages.Count; i++)
        {
            var w = TabWidth(i);
            if (y < Bounds.Y + TabHeight && x >= left && x < left + w)
            {
                SelectedIndex = i;
                return;
            }

            left += w + 2;
        }
    }

    private int TabWidth(int i) => Math.Max(58, Theme.Measure(Theme.DefaultFont, _pages[i].Header, true) + 16);

    private int TabsWidth() => Enumerable.Range(0, _pages.Count).Sum(i => TabWidth(i) + 2);
}
