using OpenPax.Forms.Drawing;
using OpenPax.Forms.Layout;

namespace OpenPax.Forms.Controls;

/// <summary>One child taller than its room, scrolled up and down with a bar at the right and the wheel.</summary>
public sealed class ScrollViewer : Element
{
    private readonly ScrollBar _bar;
    private Element? _content;

    public ScrollViewer()
    {
        _bar = Adopt(new ScrollBar());
    }

    public Element? Content
    {
        get => _content;
        set => _content = value is null ? null : Adopt(value);
    }

    public int Offset => _bar.Value;

    /// <summary>Back to the top, as for new content.</summary>
    public void ScrollToTop() => _bar.Value = 0;

    public override IEnumerable<Element> Children => _content is null ? [_bar] : [_content, _bar];

    protected override Size MeasureOverride(Size available)
    {
        var d = _content?.Measure(new Size(Math.Max(0, available.Width - ScrollBar.Thickness), Size.Infinite)) ?? default;
        _bar.Measure(available);
        return new Size(d.Width + ScrollBar.Thickness, Math.Min(d.Height, available.Height));
    }

    protected override void ArrangeOverride(Rect bounds)
    {
        var inner = bounds with { Width = Math.Max(0, bounds.Width - ScrollBar.Thickness) };
        var height = _content?.DesiredSize.Height ?? 0;
        _bar.Maximum = Math.Max(0, height - bounds.Height);
        _bar.Viewport = bounds.Height;
        _bar.Value = _bar.Value;
        _content?.Arrange(new Rect(inner.X, inner.Y - _bar.Value, inner.Width, Math.Max(height, bounds.Height)));
        _bar.Arrange(new Rect(bounds.Right - ScrollBar.Thickness, bounds.Y, ScrollBar.Thickness, bounds.Height));
    }

    protected override void RenderChildren(DrawContext dc)
    {
        dc.PushClip(Bounds);
        _content?.Render(dc);
        dc.PopClip();
        _bar.Render(dc);
    }

    public override Element? HitTest(int x, int y) => Bounds.Contains(x, y) ? base.HitTest(x, y) : null;

    protected internal override bool OnWheel(int delta) => _bar.OnWheel(delta);
}
