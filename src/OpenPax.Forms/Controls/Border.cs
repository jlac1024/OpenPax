using OpenPax.Forms.Drawing;
using OpenPax.Forms.Layout;
using OpenPax.Render.TrueColor;

namespace OpenPax.Forms.Controls;

/// <summary>One child framed: a skin of the theme's (Panel, Well, Strip), or a color and outline of one's own.</summary>
public sealed class Border : Element
{
    private Element? _child;

    public Element? Child
    {
        get => _child;
        set => _child = value is null ? null : Adopt(value);
    }

    /// <summary>"Panel" (brushed metal), "Well" (the black list well), "Strip" (the clamped field strip), or "None".</summary>
    public string Look { get; set; } = "None";

    public Color? Background { get; set; }

    public Color? BorderBrush { get; set; }

    public Thickness Padding { get; set; }

    public override IEnumerable<Element> Children => _child is null ? [] : [_child];

    protected override Size MeasureOverride(Size available) =>
        (_child?.Measure(available.Deflate(Padding)) ?? default).Inflate(Padding);

    protected override void ArrangeOverride(Rect bounds) => _child?.Arrange(bounds.Deflate(Padding));

    protected override void OnRender(DrawContext dc)
    {
        switch (Look)
        {
            case "Panel":
                dc.Panel(Bounds);
                dc.Bevel(Bounds, false);
                break;
            case "Well":
                dc.SkinOr(Theme.ListWell, Bounds, Theme.Well, true);
                break;
            case "Strip":
                dc.Strip(Bounds);
                break;
        }

        if (Background is { } b)
        {
            dc.Fill(Bounds, b);
        }

        if (BorderBrush is { } line)
        {
            dc.Outline(Bounds, line);
        }
    }
}
