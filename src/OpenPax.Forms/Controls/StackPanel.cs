using OpenPax.Forms.Layout;

namespace OpenPax.Forms.Controls;

/// <summary>Children one after another, down or across, with a gap between.</summary>
public sealed class StackPanel : Panel
{
    public Orientation Orientation { get; set; }

    public int Spacing { get; set; }

    protected override Size MeasureOverride(Size available)
    {
        var vertical = Orientation == Orientation.Vertical;
        int along = 0, across = 0, count = 0;
        foreach (var child in Shown)
        {
            var d = child.Measure(vertical ? available with { Height = Size.Infinite } : available with { Width = Size.Infinite });
            along += vertical ? d.Height : d.Width;
            across = Math.Max(across, vertical ? d.Width : d.Height);
            count++;
        }

        along += Math.Max(0, count - 1) * Spacing;
        return vertical ? new Size(across, along) : new Size(along, across);
    }

    protected override void ArrangeOverride(Rect bounds)
    {
        var vertical = Orientation == Orientation.Vertical;
        var at = vertical ? bounds.Y : bounds.X;
        foreach (var child in Shown)
        {
            var d = child.DesiredSize;
            child.Arrange(vertical ? new Rect(bounds.X, at, bounds.Width, d.Height) : new Rect(at, bounds.Y, d.Width, bounds.Height));
            at += (vertical ? d.Height : d.Width) + Spacing;
        }
    }
}
