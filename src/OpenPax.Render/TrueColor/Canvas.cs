namespace OpenPax.Render.TrueColor;

/// <summary>
/// The true-color picture the window shows at its own resolution: the
/// original screens are scaled into it and the new controls draw on it.
/// Everything is clipped to the current clip rectangle, and art is scaled
/// by whole numbers with nearest pixels so it stays sharp.
/// </summary>
public sealed class Canvas
{
    private readonly Stack<(int X, int Y, int R, int B)> _clips = new();
    private (int X, int Y, int R, int B) _clip;

    public Canvas(int width, int height)
    {
        Resize(width, height);
    }

    public int Width { get; private set; }

    public int Height { get; private set; }

    public uint[] Pixels { get; private set; } = [];

    public void Resize(int width, int height)
    {
        if (width == Width && height == Height)
        {
            return;
        }

        Width = Math.Max(1, width);
        Height = Math.Max(1, height);
        Pixels = new uint[Width * Height];
        _clips.Clear();
        _clip = (0, 0, Width, Height);
    }

    public void Clear(Color color) => Array.Fill(Pixels, color.Packed);

    /// <summary>Narrows drawing to a rectangle within the current one until <see cref="PopClip"/>.</summary>
    public void PushClip(int x, int y, int width, int height)
    {
        _clips.Push(_clip);
        _clip = (Math.Max(_clip.X, x), Math.Max(_clip.Y, y), Math.Min(_clip.R, x + width), Math.Min(_clip.B, y + height));
    }

    /// <summary>The rows the clip leaves drawable, top and bottom (exclusive), in the canvas's pixels.</summary>
    public (int Top, int Bottom) ClipRows => (_clip.Y, _clip.B);

    public void PopClip()
    {
        _clip = _clips.Count > 0 ? _clips.Pop() : (0, 0, Width, Height);
    }

    public void FillRect(int x, int y, int width, int height, Color color)
    {
        var x0 = Math.Max(x, _clip.X);
        var y0 = Math.Max(y, _clip.Y);
        var x1 = Math.Min(x + width, _clip.R);
        var y1 = Math.Min(y + height, _clip.B);
        for (var row = y0; row < y1; row++)
        {
            var line = row * Width;
            if (color.A == 255)
            {
                Array.Fill(Pixels, color.Packed, line + x0, Math.Max(0, x1 - x0));
                continue;
            }

            for (var col = x0; col < x1; col++)
            {
                Pixels[line + col] = color.Over(Pixels[line + col]);
            }
        }
    }

    /// <summary>A rectangle's outline, thickness pixels wide inside it.</summary>
    public void DrawRect(int x, int y, int width, int height, Color color, int thickness = 1)
    {
        FillRect(x, y, width, thickness, color);
        FillRect(x, y + height - thickness, width, thickness, color);
        FillRect(x, y + thickness, thickness, height - 2 * thickness, color);
        FillRect(x + width - thickness, y + thickness, thickness, height - 2 * thickness, color);
    }

    /// <summary>Part of an image (the whole by default) at a whole-number scale, clear pixels left out and translucent ones blended.</summary>
    public void DrawImage(RgbaImage image, int x, int y, int scale = 1, int srcX = 0, int srcY = 0, int srcWidth = -1, int srcHeight = -1)
    {
        srcWidth = srcWidth < 0 ? image.Width - srcX : srcWidth;
        srcHeight = srcHeight < 0 ? image.Height - srcY : srcHeight;
        DrawStretched(image, srcX, srcY, srcWidth, srcHeight, x, y, srcWidth * scale, srcHeight * scale);
    }

    /// <summary>
    /// Part of an image stretched to a rectangle with nearest pixels; the
    /// nine-slice skins use it for edges and middles at their scale.
    /// </summary>
    public void DrawStretched(RgbaImage image, int srcX, int srcY, int srcWidth, int srcHeight, int x, int y, int width, int height)
    {
        if (srcWidth <= 0 || srcHeight <= 0 || width <= 0 || height <= 0)
        {
            return;
        }

        var x0 = Math.Max(x, _clip.X);
        var y0 = Math.Max(y, _clip.Y);
        var x1 = Math.Min(x + width, _clip.R);
        var y1 = Math.Min(y + height, _clip.B);
        for (var row = y0; row < y1; row++)
        {
            var sy = srcY + (row - y) * srcHeight / height;
            var line = row * Width;
            var src = sy * image.Width;
            for (var col = x0; col < x1; col++)
            {
                var p = image.Pixels[src + srcX + (col - x) * srcWidth / width];
                var a = p >> 24;
                if (a == 255)
                {
                    Pixels[line + col] = p;
                }
                else if (a != 0)
                {
                    Pixels[line + col] = new Color(p).Over(Pixels[line + col]);
                }
            }
        }
    }

    /// <summary>
    /// An image as a nine-slice skin: corners kept, edges and middle
    /// stretched, all at the scale. Insets are in the image's own pixels.
    /// </summary>
    public void DrawNine(RgbaImage image, int x, int y, int width, int height, int left, int top, int right, int bottom, int scale = 1)
    {
        int[] sx = [0, left, image.Width - right, image.Width];
        int[] sy = [0, top, image.Height - bottom, image.Height];
        int[] dx = [x, x + left * scale, x + width - right * scale, x + width];
        int[] dy = [y, y + top * scale, y + height - bottom * scale, y + height];
        for (var j = 0; j < 3; j++)
        {
            for (var i = 0; i < 3; i++)
            {
                DrawStretched(image, sx[i], sy[j], sx[i + 1] - sx[i], sy[j + 1] - sy[j], dx[i], dy[j], dx[i + 1] - dx[i], dy[j + 1] - dy[j]);
            }
        }
    }

    /// <summary>
    /// Text in one of the game's fonts, its baseline at y, in its library's
    /// look (original, upscaled or modern) at the scale; outlined in black,
    /// white or in its color, when the library says so.
    /// </summary>
    public void DrawText(BitmapText font, string text, int x, int y, Color color, int scale = 1, bool bold = false)
    {
        var glyphs = font.Glyphs(text, x, y, scale, bold, fit: false);
        // Dark text keeping its color stands on light art by design and is left without the ring.
        if (font.Settings is { Outline: not TextOutline.None } settings
            && !(settings.Outline == TextOutline.KeepColors && BitmapText.IsDark(color.R, color.G, color.B)))
        {
            var radius = Math.Max(1, scale);
            var ring = Color.Black.WithAlpha(color.A);
            foreach (var g in glyphs)
            {
                Coverage(font.Ring(g, radius), g.Width + radius * 2, g.Height + radius * 2, g.X - radius, g.Y - radius, ring);
            }

            if (settings.Outline == TextOutline.White)
            {
                color = Color.White.WithAlpha(color.A);
            }
        }

        foreach (var g in glyphs)
        {
            Coverage(g.Coverage, g.Width, g.Height, g.X, g.Y, color);
        }
    }

    /// <summary>Anti-aliased ink (coverage 0 to 255) in a color, its top left at a point.</summary>
    public void Coverage(byte[] coverage, int width, int height, int x, int y, Color color)
    {
        for (var row = 0; row < height; row++)
        {
            var sy = y + row;
            if (sy < _clip.Y || sy >= _clip.B)
            {
                continue;
            }

            for (var col = 0; col < width; col++)
            {
                var sx = x + col;
                var c = coverage[row * width + col];
                if (c > 0 && sx >= _clip.X && sx < _clip.R)
                {
                    Pixels[sy * Width + sx] = color.WithAlpha((byte)(color.A * c / 255)).Over(Pixels[sy * Width + sx]);
                }
            }
        }
    }

    /// <summary>The width text takes at a scale, as <see cref="DrawText"/> advances.</summary>
    public static int MeasureText(BitmapText font, string text, int scale = 1, bool bold = false) =>
        font.Measure(text, bold ? 1 : 0) * scale;

    /// <summary>Another canvas laid over this one, pixel for pixel by its alpha (the same size).</summary>
    public void Overlay(Canvas top)
    {
        var n = Math.Min(Pixels.Length, top.Pixels.Length);
        for (var i = 0; i < n; i++)
        {
            Pixels[i] = new Color(top.Pixels[i]).Over(Pixels[i]);
        }
    }

    /// <summary>The palette picture of the original screens stretched over a rectangle with nearest pixels (screenshots).</summary>
    public void DrawClassic(Framebuffer frame, Data.Art.Palette palette, int x, int y, int width, int height)
    {
        var colors = new uint[256];
        for (var i = 0; i < 256; i++)
        {
            colors[i] = Color.Of(palette.Colors[i]).Packed;
        }

        var x0 = Math.Max(x, _clip.X);
        var y0 = Math.Max(y, _clip.Y);
        var x1 = Math.Min(x + width, _clip.R);
        var y1 = Math.Min(y + height, _clip.B);
        for (var row = y0; row < y1; row++)
        {
            var src = (row - y) * frame.PixelHeight / height * frame.PixelWidth;
            var line = row * Width;
            for (var col = x0; col < x1; col++)
            {
                Pixels[line + col] = colors[frame.Pixels[src + (col - x) * frame.PixelWidth / width]];
            }
        }
    }
}
