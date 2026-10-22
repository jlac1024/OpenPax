using OpenPax.Data.Views;
using OpenPax.Render;

namespace OpenPax.Ui.Widgets;

/// <summary>Class 8024, text set by code, word wrapped into the widget.</summary>
public sealed class WrappedTextPane(ViewNode node, int left, int top, TextFieldDetails details) : Pane(node, left, top)
{
    public TextFieldDetails Details { get; } = details;

    public string Text { get; set; } = "";

    public bool Highlighted { get; set; }

    protected override void Draw(Framebuffer target)
    {
        var style = Details.Style;
        var color = Highlighted ? (byte)16 : (byte)style.Color;
        WrappedText.Draw(target, Assets.Fonts.ForIndex(style.Font), Text, Left, Top, Width, Height,
            style.Style, style.Align, color, (byte)style.ShadowColor);
    }
}
