using OpenPax.Render;
using OpenPax.Render.TrueColor;

namespace OpenPax.Forms.Drawing;

/// <summary>
/// How the controls look: the game's fonts, colors taken from its
/// network screens' palette, and skins cut from their art. A mod may make
/// its own. Any skin left null is drawn with plain bevels instead.
/// </summary>
public sealed class Theme
{
    public required Func<int, BitmapText> Fonts { get; init; }

    public required IArtSource Art { get; init; }

    /// <summary>Canvas pixels per layout unit, set by the host before each layout; text is measured as drawn at it.</summary>
    public int Scale { get; set; } = 1;

    /// <summary>A line's width in layout units, as it is drawn (the modern font's own width when that is the look).</summary>
    public int Measure(int font, string text, bool bold = false) => Fonts(font).MeasureDrawn(text, bold, Scale);

    /// <summary>A line's height in layout units as it is drawn at <see cref="Scale"/>; controls size themselves by it.</summary>
    public int LineHeight(int font) => Math.Max(1, Fonts(font).DrawnLineHeight(Scale));

    /// <summary>How far a line reaches above its baseline, in layout units as drawn.</summary>
    public int Ascent(int font, bool bold = false) => Fonts(font).DrawnAscent(bold, Scale);

    /// <summary>The game's font index used when an element names none (1 is pft10).</summary>
    public int DefaultFont { get; init; } = 1;

    public Color Text { get; init; } = Color.White;

    public Color DimText { get; init; } = Color.Rgb(150, 150, 150);

    /// <summary>Text standing on the panels and the backdrop rather than in a control's well.</summary>
    public Color PanelText { get; init; } = Color.Rgb(225, 225, 225);

    public Color PanelDimText { get; init; } = Color.Rgb(150, 150, 150);

    public Color DisabledText { get; init; } = Color.Rgb(90, 90, 90);

    public Color Heading { get; init; } = Color.Rgb(230, 40, 40);

    public Color ButtonText { get; init; } = Color.Rgb(40, 40, 40);

    public Color ButtonPressedText { get; init; } = Color.Rgb(220, 30, 30);

    public Color Accent { get; init; } = Color.Rgb(40, 200, 40);

    public Color Selection { get; init; } = Color.Rgb(30, 110, 30);

    public Color Well { get; init; } = Color.Black;

    public Color Light { get; init; } = Color.Rgb(170, 170, 170);

    public Color Shade { get; init; } = Color.Rgb(40, 40, 40);

    public Color Panel { get; init; } = Color.Rgb(110, 110, 110);

    public Skin? PanelSkin { get; init; }

    /// <summary>A line drawn round each panel, just outside it, so a dark panel stands clear of a dark backdrop; none when null.</summary>
    public Color? PanelFrame { get; init; }

    /// <summary>A shadow cast below and right of each panel onto the backdrop; none when null.</summary>
    public Color? PanelShadow { get; init; }

    /// <summary>A picture repeated behind whole forms at the canvas's own pixels (a starfield), when there is one.</summary>
    public RgbaImage? Backdrop { get; init; }

    /// <summary>A moving starfield drawn behind whole forms instead of <see cref="Backdrop"/>; null for the still picture.</summary>
    public WarpField? Warp { get; set; }

    public Skin? Button { get; init; }

    public Skin? ButtonPressed { get; init; }

    public Skin? Field { get; init; }

    /// <summary>Where text starts inside a field (text box, combo box): clear of the field skin's end cap with room to breathe.</summary>
    public int FieldTextInset => (Field?.Left ?? 2) + 6;

    public Skin? ListWell { get; init; }

    public Skin? HeaderStrip { get; init; }

    public Skin? Tab { get; init; }

    public Skin? TabPressed { get; init; }

    public RgbaImage? CheckOff { get; init; }

    public RgbaImage? CheckOn { get; init; }

    public RgbaImage? RadioOff { get; init; }

    public RgbaImage? RadioOn { get; init; }

    public RgbaImage? ArrowLeft { get; init; }

    public RgbaImage? ArrowLeftPressed { get; init; }

    public RgbaImage? ArrowRight { get; init; }

    public RgbaImage? ArrowRightPressed { get; init; }

    /// <summary>The box between a spinner's arrows, as tall as they are; null for a plain well.</summary>
    public Skin? SpinBox { get; init; }

    public RgbaImage? ArrowUp { get; init; }

    public RgbaImage? ArrowDown { get; init; }

    public Skin? ScrollTrack { get; init; }

    public Skin? ScrollThumb { get; init; }
}
