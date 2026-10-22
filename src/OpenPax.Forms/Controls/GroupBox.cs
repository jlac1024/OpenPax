using OpenPax.Forms.Drawing;
using OpenPax.Forms.Layout;

namespace OpenPax.Forms.Controls;

/// <summary>A titled frame: the header in the game's red heading letters on its clamped strip, the content below on brushed metal.</summary>
public sealed class GroupBox : Element
{
    private Element? _content;

    public string Header { get; set; } = "";

    public Element? Content
    {
        get => _content;
        set => _content = value is null ? null : Adopt(value);
    }

    public Thickness Padding { get; set; } = new(6);

    public override IEnumerable<Element> Children => _content is null ? [] : [_content];

    private int HeaderHeight => Theme.LineHeight(Theme.DefaultFont) + 8;

    protected override Size MeasureOverride(Size available)
    {
        var inner = new Size(available.Width, Math.Max(0, available.Height - HeaderHeight)).Deflate(Padding);
        var d = _content?.Measure(inner) ?? default;
        var header = Theme.Measure(Theme.DefaultFont, Header, true) + 24;
        return new Size(Math.Max(header, d.Width + Padding.Horizontal), d.Height + Padding.Vertical + HeaderHeight);
    }

    protected override void ArrangeOverride(Rect bounds) =>
        _content?.Arrange(new Rect(bounds.X, bounds.Y + HeaderHeight, bounds.Width, Math.Max(0, bounds.Height - HeaderHeight)).Deflate(Padding));

    protected override void OnRender(DrawContext dc)
    {
        var body = new Rect(Bounds.X, Bounds.Y + HeaderHeight - 2, Bounds.Width, Math.Max(0, Bounds.Height - HeaderHeight + 2));
        dc.Panel(body);
        dc.Bevel(body, false);
        var strip = Bounds with { Height = HeaderHeight };
        dc.Strip(strip);
        var w = dc.Measure(Header, Theme.DefaultFont, true);
        dc.Text(Header, strip.X + (strip.Width - w) / 2, strip.Y + 4, Theme.Heading, Theme.DefaultFont, true);
    }
}
