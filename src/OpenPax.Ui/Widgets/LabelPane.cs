using OpenPax.Data.Views;
using OpenPax.Render;

namespace OpenPax.Ui.Widgets;

/// <summary>Class 8025, a line of text from a STR# list.</summary>
public sealed class LabelPane(ViewNode node, int left, int top, LabelDetails details) : Pane(node, left, top)
{
    public LabelDetails Details { get; } = details;

    /// <summary>Text set by code in place of the STR# entry, if any.</summary>
    public string? Text { get; set; }

    /// <summary>Byte +0x75: drawn in color 16 while set.</summary>
    public bool Highlighted { get; set; }

    /// <summary>Byte +0x76: drawn in color 22 while set (set by code).</summary>
    public bool Marked { get; set; }

    /// <summary>A color set by code in place of the label's own, if any.</summary>
    public byte? Color { get; set; }

    protected override void Draw(Framebuffer target)
    {
        var style = Details.Style;
        var font = Assets.Fonts.ForIndex(style.Font);
        var text = Text ?? Assets.Strings.Get(Details.Text);
        var color = !Enabled ? (byte)10 : Highlighted ? (byte)16 : Marked ? (byte)22 : Color ?? (byte)style.Color;
        var x = TextPlacement.AnchorX(style.Align, Left, Width);
        var y = TextPlacement.Baseline(font, style.Style, Top + Height);
        font.Draw(target, text, x, y, style.Style, style.Align, color, (byte)style.ShadowColor);
    }
}
