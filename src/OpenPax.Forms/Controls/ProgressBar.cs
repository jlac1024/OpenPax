using OpenPax.Forms.Drawing;
using OpenPax.Forms.Layout;

namespace OpenPax.Forms.Controls;

/// <summary>How far along something is: a sunken well filled green from the left.</summary>
public sealed class ProgressBar : Element
{
    private int _value;

    public int Maximum { get; set; } = 100;

    public int Value
    {
        get => _value;
        set => Set(ref _value, Math.Clamp(value, 0, Maximum));
    }

    protected override Size MeasureOverride(Size available) => new(100, 10);

    protected override void OnRender(DrawContext dc)
    {
        dc.Fill(Bounds, Theme.Well);
        dc.Bevel(Bounds, true);
        var inner = Bounds.Deflate(new Thickness(1));
        dc.Fill(inner with { Width = Maximum <= 0 ? 0 : inner.Width * Value / Maximum }, Theme.Accent);
    }
}
