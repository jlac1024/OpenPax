using OpenPax.Forms.Drawing;
using OpenPax.Forms.Layout;

namespace OpenPax.Forms.Controls;

/// <summary>A sunken line across (or down, in a horizontal stack) to set parts apart.</summary>
public sealed class Separator : Element
{
    protected override Size MeasureOverride(Size available) => new(2, 2);

    protected override void OnRender(DrawContext dc)
    {
        var across = Bounds.Width >= Bounds.Height;
        dc.Bevel(across ? new Rect(Bounds.X, Bounds.Y + Bounds.Height / 2 - 1, Bounds.Width, 2) : new Rect(Bounds.X + Bounds.Width / 2 - 1, Bounds.Y, 2, Bounds.Height), true);
    }
}
