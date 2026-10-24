using OpenPax.Forms.Drawing;
using OpenPax.Forms.Layout;
using OpenPax.Render.TrueColor;

namespace OpenPax.Forms.Controls;

/// <summary>Text: one line, or wrapped to its width.</summary>
public sealed class TextBlock : Element
{
    private string _text = "";
    private IReadOnlyList<string> _lines = [];

    // The last wrap, kept while the text, width and look are the same: a long text wraps once, not every frame.
    // The probe's width stands for the font's look, which the player can change (Options, text font).
    private (string Text, int Width, int Font, bool Bold, int Scale, int Probe, Size Size)? _wrapped;

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
        var wrapWidth = TextWrapping && available.Width < Size.Infinite ? available.Width : -1;
        var probe = Theme.Measure(FontIndex, "Wg0", Bold);
        if (_wrapped is { } w && ReferenceEquals(w.Text, _text) && w.Width == wrapWidth && w.Font == FontIndex && w.Bold == Bold && w.Scale == Theme.Scale && w.Probe == probe)
        {
            return w.Size;
        }

        int Width(string line) => Theme.Measure(FontIndex, line, Bold);
        _lines = wrapWidth >= 0 ? TextWrap.Lines(Width, _text, wrapWidth) : _text.Split('\n');
        var width = _lines.Count == 0 ? 0 : _lines.Max(Width);
        var size = new Size(width + 1, _lines.Count * Theme.LineHeight(FontIndex));
        _wrapped = (_text, wrapWidth, FontIndex, Bold, Theme.Scale, probe, size);
        return size;
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
        var lineHeight = dc.LineHeight(FontIndex);
        dc.PushClip(Bounds);
        foreach (var line in _lines)
        {
            // Lines scrolled out of view are not drawn (a long text in a ScrollViewer).
            if (!dc.RowsVisible(y, lineHeight))
            {
                y += lineHeight;
                continue;
            }

            var w = dc.Measure(line, FontIndex, Bold);
            var x = TextAlignment switch
            {
                TextAlignment.Center => Bounds.X + (Bounds.Width - w) / 2,
                TextAlignment.Right => Bounds.Right - w,
                _ => Bounds.X,
            };
            dc.Text(line, x, y, color, FontIndex, Bold);
            y += lineHeight;
        }

        dc.PopClip();
    }
}
