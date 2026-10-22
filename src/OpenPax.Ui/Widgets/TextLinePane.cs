using OpenPax.Data.Views;
using OpenPax.Render;

namespace OpenPax.Ui.Widgets;

/// <summary>Class 8028, one line of text set by code.</summary>
public sealed class TextLinePane(ViewNode node, int left, int top, TextFieldDetails details) : Pane(node, left, top)
{
    public TextFieldDetails Details { get; } = details;

    public string Text { get; set; } = "";

    public bool Highlighted { get; set; }

    /// <summary>A color set by code in place of the layout's (+0x5C), or null.</summary>
    public byte? Color { get; set; }

    protected override void Draw(Framebuffer target)
    {
        var style = Details.Style;
        var font = Assets.Fonts.ForIndex(style.Font);
        var color = Highlighted ? (byte)16 : Color ?? (byte)style.Color;
        font.Draw(target, Text, TextPlacement.AnchorX(style.Align, Left, Width), TextPlacement.Baseline(font, style.Style, Top + Height),
            style.Style, style.Align, color, (byte)style.ShadowColor);
    }
}
