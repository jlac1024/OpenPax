using OpenPax.Forms.Layout;

namespace OpenPax.Forms.Controls;

/// <summary>Children at fixed places (Canvas.Left, Canvas.Top) at their own size; "Canvas" in markup.</summary>
public sealed class CanvasPanel : Panel
{
    protected override Size MeasureOverride(Size available)
    {
        var size = new Size(0, 0);
        foreach (var child in Shown)
        {
            var d = child.Measure(new Size(Size.Infinite, Size.Infinite));
            size = new Size(Math.Max(size.Width, child.CanvasLeft + d.Width), Math.Max(size.Height, child.CanvasTop + d.Height));
        }

        return size;
    }

    protected override void ArrangeOverride(Rect bounds)
    {
        foreach (var child in Shown)
        {
            child.Arrange(new Rect(bounds.X + child.CanvasLeft, bounds.Y + child.CanvasTop, child.DesiredSize.Width, child.DesiredSize.Height));
        }
    }
}
