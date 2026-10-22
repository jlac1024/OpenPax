using OpenPax.Forms.Drawing;
using OpenPax.Forms.Layout;
using OpenPax.Render.TrueColor;

namespace OpenPax.Forms.Controls;

/// <summary>Text: one line, or wrapped to its width.</summary>
public sealed class TextBlock : Element
{
    private string _text = "";
    private IReadOnlyList<string> _lines = [];

    public string Text
    {
        get => _text;
        set => Set(ref _text, value ?? "");
    }

    /// <summary>The color, or the theme's text color. "Heading" looks are red as the game's titles.</summary>
    public Color? Foreground { get; set; }

    /// <summary>
    /// "Normal" and "Dim" for text on the metal panels, "Bright" and
    /// "Muted" for text in black wells, "Heading" for the game's red titles.
    /// </summary>
    public string Look { get; set; } = "Normal";

    /// <summary>The game's font index (0 to 6); null for the theme's.</summary>
    public int? Font { get; set; }

    public bool Bold { get; set; }

    public bool TextWrapping { get; set; }

    public TextAlignment TextAlignment { get; set; }

    private int FontIndex => Font ?? Theme.DefaultFont;

    protected override Size MeasureOverride(Size available)
    {
        var font = Theme.Fonts(FontIndex);
        int Width(string line) => Theme.Measure(FontIndex, line, Bold);
        _lines = TextWrapping && available.Width < Size.Infinite ? TextWrap.Lines(Width, _text, available.Width) : _text.Split('\n');
        var width = _lines.Count == 0 ? 0 : _lines.Max(Width);
        return new Size(width + 1, _lines.Count * Theme.LineHeight(FontIndex));
    }

    protected override void OnRender(DrawContext dc)
    {
        var color = Foreground ?? Look switch
        {
            "Heading" => Theme.Heading,
            "Dim" => Theme.PanelDimText,
            "Bright" => Theme.Text,
            "Muted" => Theme.DimText,
            _ => Theme.PanelText,
        };
        var y = Bounds.Y;
        dc.PushClip(Bounds);
        foreach (var line in _lines)
        {
            var w = dc.Measure(line, FontIndex, Bold);
            var x = TextAlignment switch
            {
                TextAlignment.Center => Bounds.X + (Bounds.Width - w) / 2,
                TextAlignment.Right => Bounds.Right - w,
                _ => Bounds.X,
            };
            dc.Text(line, x, y, color, FontIndex, Bold);
            y += dc.LineHeight(FontIndex);
        }

        dc.PopClip();
    }
}
