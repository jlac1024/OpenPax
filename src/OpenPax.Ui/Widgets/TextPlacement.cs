using OpenPax.Render;

namespace OpenPax.Ui.Widgets;

/// <summary>Where single line widgets put their text.</summary>
internal static class TextPlacement
{
    /// <summary>
    /// The baseline of a line whose cell sits on the widget's bottom edge:
    /// bottom - (line height - ascent).
    /// </summary>
    public static int Baseline(BitmapText font, int style, int bottom)
    {
        return bottom - (font.LineHeight - font.AscentFor(style));
    }

    /// <summary>The x text is aligned at: left, the middle or right edge.</summary>
    public static int AnchorX(int align, int left, int width)
    {
        return align switch
        {
            1 => (left + left + width) / 2,
            2 => left + width,
            _ => left,
        };
    }
}
