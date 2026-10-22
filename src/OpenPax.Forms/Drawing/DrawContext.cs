using OpenPax.Forms.Layout;
using OpenPax.Render;
using OpenPax.Render.TrueColor;

namespace OpenPax.Forms.Drawing;

/// <summary>
/// Drawing in layout units: each unit is <see cref="Scale"/> canvas pixels,
/// so the game's pixel art and fonts stay sharp at any window size.
/// </summary>
public sealed class DrawContext(Canvas canvas, Theme theme, int scale, int originX = 0, int originY = 0, long now = 0)
{
    public Canvas Canvas { get; } = canvas;

    public Theme Theme { get; } = theme;

    public int Scale { get; } = scale;

    /// <summary>Elements drawn while this is false look disabled.</summary>
    public bool Enabled { get; set; } = true;

    public void Fill(Rect r, Color color) => Canvas.FillRect(X(r.X), Y(r.Y), r.Width * Scale, r.Height * Scale, color);

    public void Outline(Rect r, Color color) => Canvas.DrawRect(X(r.X), Y(r.Y), r.Width * Scale, r.Height * Scale, color, Scale);

    /// <summary>A raised (or sunken) one unit bevel round a rectangle.</summary>
    public void Bevel(Rect r, bool sunken)
    {
        var (top, bottom) = sunken ? (Theme.Shade, Theme.Light) : (Theme.Light, Theme.Shade);
        Fill(r with { Height = 1 }, top);
        Fill(r with { Width = 1 }, top);
        Fill(new Rect(r.X, r.Bottom - 1, r.Width, 1), bottom);
        Fill(new Rect(r.Right - 1, r.Y, 1, r.Height), bottom);
    }

    public void Skin(Skin skin, Rect r)
    {
        if (skin.Left + skin.Top + skin.Right + skin.Bottom == 0)
        {
            Canvas.DrawStretched(skin.Image, 0, 0, skin.Image.Width, skin.Image.Height, X(r.X), Y(r.Y), r.Width * Scale, r.Height * Scale);
            return;
        }

        Canvas.DrawNine(skin.Image, X(r.X), Y(r.Y), r.Width * Scale, r.Height * Scale, skin.Left, skin.Top, skin.Right, skin.Bottom, Scale);
    }

    /// <summary>A skin when the theme has one, else a plain bevelled box in a color.</summary>
    public void SkinOr(Skin? skin, Rect r, Color fill, bool sunken)
    {
        if (skin is not null)
        {
            Skin(skin, r);
            return;
        }

        Fill(r, fill);
        Bevel(r, sunken);
    }

    /// <summary>A picture at its own size times the scale, its top left at a point.</summary>
    public void Image(RgbaImage image, int x, int y) => Canvas.DrawImage(image, X(x), Y(y), Scale);

    /// <summary>A picture centered in a rectangle at its own size.</summary>
    public void ImageCentered(RgbaImage image, Rect r) => Image(image, r.X + (r.Width - image.Width) / 2, r.Y + (r.Height - image.Height) / 2);

    /// <summary>A panel: the theme's framed well for its metal edge, filled plain inside with the panel color; else that color with a bevel. The theme's frame line and shadow go round it.</summary>
    public void Panel(Rect r)
    {
        if (Theme.PanelShadow is { } shadow)
        {
            Fill(new Rect(r.X + 3, r.Bottom + 1, r.Width - 1, 3), shadow);
            Fill(new Rect(r.Right + 1, r.Y + 3, 3, r.Height + 1), shadow);
        }

        if (Theme.PanelFrame is { } frame)
        {
            Outline(new Rect(r.X - 1, r.Y - 1, r.Width + 2, r.Height + 2), frame);
        }

        SkinOr(Theme.ListWell, r, Theme.Panel, false);
        if (Theme.ListWell is not null)
        {
            Fill(r.Deflate(new Thickness(3)), Theme.Panel);
        }
    }

    /// <summary>A header strip, with the theme's panel frame line round it so it stands clear of the backdrop as panels do.</summary>
    public void Strip(Rect r)
    {
        if (Theme.PanelFrame is { } frame)
        {
            Outline(new Rect(r.X - 1, r.Y - 1, r.Width + 2, r.Height + 2), frame);
        }

        SkinOr(Theme.HeaderStrip, r, Theme.Well, true);
    }

    /// <summary>
    /// What lies behind a whole form: the theme's backdrop repeated at the
    /// canvas's own pixels (so a starfield stays pin sharp), else the well color;
    /// or, when the theme has one, its moving starfield.
    /// </summary>
    public void Backdrop(Rect r)
    {
        if (Theme.Warp is { } warp)
        {
            Fill(r, Color.Black);
            Canvas.PushClip(X(r.X), Y(r.Y), r.Width * Scale, r.Height * Scale);
            warp.Draw(Canvas, X(r.X), Y(r.Y), r.Width * Scale, r.Height * Scale, now, Scale);
            Canvas.PopClip();
            return;
        }

        if (Theme.Backdrop is not { } tile)
        {
            Fill(r, Theme.Well);
            return;
        }

        var (x0, y0, w, h) = (X(r.X), Y(r.Y), r.Width * Scale, r.Height * Scale);
        Canvas.PushClip(x0, y0, w, h);
        for (var y = y0; y < y0 + h; y += tile.Height)
        {
            for (var x = x0; x < x0 + w; x += tile.Width)
            {
                Canvas.DrawImage(tile, x, y);
            }
        }

        Canvas.PopClip();
    }

    public BitmapText Font(int index) => Theme.Fonts(index);

    /// <summary>Text with its top at y (not its baseline), dimmed when disabled.</summary>
    public void Text(string text, int x, int y, Color color, int font, bool bold = false)
    {
        var f = Font(font);
        Canvas.DrawText(f, text, X(x), Y(y) + Theme.Ascent(font, bold) * Scale, Enabled ? color : Theme.DisabledText, Scale, bold);
    }

    public int Measure(string text, int font, bool bold = false) => Theme.Measure(font, text, bold);

    public int LineHeight(int font) => Theme.LineHeight(font);

    public void PushClip(Rect r) => Canvas.PushClip(X(r.X), Y(r.Y), r.Width * Scale, r.Height * Scale);

    public void PopClip() => Canvas.PopClip();

    private int X(int x) => originX + x * Scale;

    private int Y(int y) => originY + y * Scale;
}
