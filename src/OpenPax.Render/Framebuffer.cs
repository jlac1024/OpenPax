using OpenPax.Data.Art;

namespace OpenPax.Render;

/// <summary>
/// The 8-bit screen the game draws into, as the original drew into its
/// 640x480 palettized DirectDraw surface. Turned into RGBA only when shown.
/// Coordinates are always the original's; at scale 2 the pixels are twice
/// as many each way and sprites draw their 2x pictures where they have them
/// (decisions.md, "Upscaled art and a modern font").
/// </summary>
public sealed class Framebuffer
{
    private readonly Dictionary<int, byte> _blends = [];
    private int _clipLeft;
    private int _clipTop;
    private int _clipRight;
    private int _clipBottom;

    public Framebuffer(int width, int height, int scale = 1)
    {
        Width = width;
        Height = height;
        Scale = Math.Max(1, scale);
        Pixels = new byte[width * Scale * height * Scale];
        ResetClip();
    }

    /// <summary>The size in the original's coordinates.</summary>
    public int Width { get; }

    public int Height { get; }

    /// <summary>Pixels per coordinate each way: 1, or 2 for the upscaled screens.</summary>
    public int Scale { get; }

    public int PixelWidth => Width * Scale;

    /// <summary>The palette the frame will be shown with; anti-aliased text blends through it. Null: no blending.</summary>
    public Palette? Palette
    {
        get;
        set
        {
            if (field != value)
            {
                field = value;
                _blends.Clear();
            }
        }
    }

    public int PixelHeight => Height * Scale;

    public byte[] Pixels { get; }

    public void Clear(byte index)
    {
        Array.Fill(Pixels, index);
    }

    /// <summary>
    /// Limits drawing to a rectangle within the screen, as the original sets
    /// its port's clip to each widget's own rect before drawing it.
    /// </summary>
    public void SetClip(int x, int y, int width, int height)
    {
        _clipLeft = Math.Max(x, 0);
        _clipTop = Math.Max(y, 0);
        _clipRight = Math.Min(x + width, Width);
        _clipBottom = Math.Min(y + height, Height);
    }

    /// <summary>The clip rectangle now in force.</summary>
    public (int X, int Y, int Width, int Height) Clip => (_clipLeft, _clipTop, _clipRight - _clipLeft, _clipBottom - _clipTop);

    public void ResetClip()
    {
        (_clipLeft, _clipTop, _clipRight, _clipBottom) = (0, 0, Width, Height);
    }

    /// <summary>Draws a sprite with its top left at (x, y), clipped.</summary>
    public void Draw(Sprite sprite, int x, int y)
    {
        if (Scale > 1)
        {
            DrawScaled(sprite, x, y, null);
            return;
        }

        for (var row = 0; row < sprite.Height; row++)
        {
            var sy = y + row;
            if (sy < _clipTop || sy >= _clipBottom)
            {
                continue;
            }

            for (var col = 0; col < sprite.Width; col++)
            {
                var sx = x + col;
                var p = sprite.Pixels[row * sprite.Width + col];
                if (sx >= _clipLeft && sx < _clipRight && p != Sprite.TransparentIndex)
                {
                    Pixels[sy * Width + sx] = p;
                }
            }
        }
    }

    /// <summary>Paints every opaque pixel of a sprite in one color, its top left at (x, y), clipped.</summary>
    public void DrawTinted(Sprite sprite, int x, int y, byte color)
    {
        if (Scale > 1)
        {
            DrawScaled(sprite, x, y, color);
            return;
        }

        for (var row = 0; row < sprite.Height; row++)
        {
            var sy = y + row;
            if (sy < _clipTop || sy >= _clipBottom)
            {
                continue;
            }

            for (var col = 0; col < sprite.Width; col++)
            {
                var sx = x + col;
                if (sx >= _clipLeft && sx < _clipRight && sprite.Pixels[row * sprite.Width + col] != Sprite.TransparentIndex)
                {
                    Pixels[sy * Width + sx] = color;
                }
            }
        }
    }

    /// <summary>
    /// Sets every pixel where a 1-bit mask (stored as bytes, 1 = ink) is set,
    /// copying the rectangle (maskX, maskY, width, height) of the mask to (x, y).
    /// </summary>
    public void DrawMask(byte[] mask, int maskStride, int maskX, int maskY, int width, int height, int x, int y, byte color)
    {
        if (Scale > 1)
        {
            for (var row = 0; row < height; row++)
            {
                for (var col = 0; col < width; col++)
                {
                    if (mask[(maskY + row) * maskStride + maskX + col] == 1)
                    {
                        Plot(x + col, y + row, color);
                    }
                }
            }

            return;
        }

        for (var row = 0; row < height; row++)
        {
            var sy = y + row;
            if (sy < _clipTop || sy >= _clipBottom)
            {
                continue;
            }

            for (var col = 0; col < width; col++)
            {
                var sx = x + col;
                if (sx >= _clipLeft && sx < _clipRight && mask[(maskY + row) * maskStride + maskX + col] == 1)
                {
                    Pixels[sy * Width + sx] = color;
                }
            }
        }
    }

    /// <summary>A one pixel line between two points, both ends drawn, clipped.</summary>
    public void DrawLine(int x0, int y0, int x1, int y1, byte color)
    {
        // Bresenham; the original's line routine is not read
        // yet, so single pixels may differ from it.
        var dx = Math.Abs(x1 - x0);
        var dy = -Math.Abs(y1 - y0);
        var sx = x0 < x1 ? 1 : -1;
        var sy = y0 < y1 ? 1 : -1;
        var err = dx + dy;
        while (true)
        {
            Plot(x0, y0, color);

            if (x0 == x1 && y0 == y1)
            {
                return;
            }

            var e2 = 2 * err;
            if (e2 >= dy)
            {
                err += dy;
                x0 += sx;
            }

            if (e2 <= dx)
            {
                err += dx;
                y0 += sy;
            }
        }
    }

    public void FillRect(int x, int y, int width, int height, byte color)
    {
        var x0 = Math.Max(x, _clipLeft) * Scale;
        var x1 = Math.Min(x + width, _clipRight) * Scale;
        for (var sy = Math.Max(y, _clipTop) * Scale; sy < Math.Min(y + height, _clipBottom) * Scale; sy++)
        {
            if (x1 > x0)
            {
                Pixels.AsSpan(sy * PixelWidth + x0, x1 - x0).Fill(color);
            }
        }
    }

    /// <summary>A 1-bit mask (1 = ink) at pixel coordinates rather than the original's, clipped: the upscaled game fonts.</summary>
    public void DrawPixelMask(byte[] mask, int stride, int maskX, int maskY, int width, int height, int px, int py, byte color)
    {
        var (left, top, right, bottom) = (_clipLeft * Scale, _clipTop * Scale, _clipRight * Scale, _clipBottom * Scale);
        for (var row = 0; row < height; row++)
        {
            var sy = py + row;
            if (sy < top || sy >= bottom)
            {
                continue;
            }

            for (var col = 0; col < width; col++)
            {
                var sx = px + col;
                if (sx >= left && sx < right && mask[(maskY + row) * stride + maskX + col] == 1)
                {
                    Pixels[sy * PixelWidth + sx] = color;
                }
            }
        }
    }

    /// <summary>
    /// Anti-aliased ink (coverage 0 to 255) at pixel coordinates, clipped.
    /// Partly covered pixels take the palette entry nearest to the ink
    /// mixed with what is under it, so the frame stays palette indices.
    /// </summary>
    public void DrawCoverage(byte[] coverage, int width, int height, int px, int py, byte color)
    {
        var (left, top, right, bottom) = (_clipLeft * Scale, _clipTop * Scale, _clipRight * Scale, _clipBottom * Scale);
        for (var row = 0; row < height; row++)
        {
            var sy = py + row;
            if (sy < top || sy >= bottom)
            {
                continue;
            }

            for (var col = 0; col < width; col++)
            {
                var sx = px + col;
                var c = coverage[row * width + col];
                if (sx < left || sx >= right || c < 24)
                {
                    continue;
                }

                ref var p = ref Pixels[sy * PixelWidth + sx];
                if (Palette is null)
                {
                    p = c >= 128 ? color : p;
                }
                else
                {
                    p = c >= 232 ? color : Blend(color, p, c);
                }
            }
        }
    }

    /// <summary>How colorful a pixel rectangle is through the palette: the mean of each pixel's brightest less dullest channel, 0 for gray, sampled every other pixel.</summary>
    public int Chroma(int px, int py, int width, int height)
    {
        if (Palette is null)
        {
            return 0;
        }

        long sum = 0;
        var count = 0;
        for (var y = Math.Max(0, py); y < Math.Min(PixelHeight, py + height); y += 2)
        {
            for (var x = Math.Max(0, px); x < Math.Min(PixelWidth, px + width); x += 2)
            {
                var c = Palette.Colors[Pixels[y * PixelWidth + x]];
                sum += Math.Max(c.R, Math.Max(c.G, c.B)) - Math.Min(c.R, Math.Min(c.G, c.B));
                count++;
            }
        }

        return count == 0 ? 0 : (int)(sum / count);
    }

    /// <summary>The palette entry nearest to a color (the never-drawn index aside); without a palette, 0 for dark and 255 for light.</summary>
    public byte Nearest(byte r, byte g, byte b)
    {
        if (Palette is null)
        {
            return r + g + b < 384 ? (byte)0 : (byte)255;
        }

        var key = (1 << 30) | (r << 16) | (g << 8) | b;
        if (_blends.TryGetValue(key, out var known))
        {
            return known;
        }

        var colors = Palette.Colors;
        var best = 0;
        var bestDistance = int.MaxValue;
        for (var i = 0; i < colors.Count; i++)
        {
            if (i == Sprite.TransparentIndex)
            {
                continue;
            }

            var c = colors[i];
            var d = 2 * (c.R - r) * (c.R - r) + 4 * (c.G - g) * (c.G - g) + 3 * (c.B - b) * (c.B - b);
            if (d < bestDistance)
            {
                (best, bestDistance) = (i, d);
            }
        }

        _blends[key] = (byte)best;
        return (byte)best;
    }

    /// <summary>The palette entry nearest to ink over a background at a coverage, in eight steps, remembered.</summary>
    private byte Blend(byte ink, byte under, int coverage)
    {
        var step = coverage * 8 / 256;
        var key = (ink << 16) | (under << 8) | step;
        if (_blends.TryGetValue(key, out var known))
        {
            return known;
        }

        var colors = Palette!.Colors; // Blend is only reached with a palette.
        var (a, b) = (colors[ink], colors[under]);
        var t = (step + 0.5) / 8;
        var (r, g, bl) = (a.R * t + b.R * (1 - t), a.G * t + b.G * (1 - t), a.B * t + b.B * (1 - t));
        var best = under;
        var bestDistance = double.MaxValue;
        for (var i = 0; i < colors.Count; i++)
        {
            if (i == Sprite.TransparentIndex)
            {
                continue;
            }

            var c = colors[i];
            var d = 2 * Math.Pow(c.R - r, 2) + 4 * Math.Pow(c.G - g, 2) + 3 * Math.Pow(c.B - bl, 2);
            if (d < bestDistance)
            {
                (best, bestDistance) = ((byte)i, d);
            }
        }

        _blends[key] = best;
        return best;
    }

    /// <summary>One of the original's pixels, a Scale-sized square, clipped.</summary>
    private void Plot(int x, int y, byte color)
    {
        if (x < _clipLeft || x >= _clipRight || y < _clipTop || y >= _clipBottom)
        {
            return;
        }

        for (var row = 0; row < Scale; row++)
        {
            Pixels.AsSpan((y * Scale + row) * PixelWidth + x * Scale, Scale).Fill(color);
        }
    }

    /// <summary>
    /// A sprite at a scale above 1: its 2x picture pixel for pixel when it
    /// has one and the scale is 2, else each of its pixels as a square. With
    /// a color, every drawn pixel is that color (a tint).
    /// </summary>
    /// <summary>
    /// Draws a rectangle of a sprite (its own pixels) into a rectangle of the
    /// screen (docs/ui/widescreen.md), stretched or squeezed to fit by
    /// nearest pixel, so the art there (brushed metal, an empty view) just
    /// grows, with no joints. Rows in a tiled band repeat at the band's period
    /// across instead of stretching (a ruler keeps its ticks' spacing).
    /// Opaque pixels only, clipped, at the frame's scale, from the sprite's
    /// upscaled picture when the scale is even.
    /// </summary>
    /// <param name="mirror">Left for right, for extending a picture past its edge with a reflection of itself.</param>
    public void DrawRegion(Sprite sprite, int srcX, int srcY, int srcWidth, int srcHeight, int x, int y, int width, int height, IReadOnlyList<(int Top, int Bottom, int Period)>? tiled = null, bool mirror = false)
    {
        if (srcWidth <= 0 || srcHeight <= 0 || width <= 0 || height <= 0)
        {
            return;
        }

        var (source, per) = Source(sprite);
        var (left, top, right, bottom) = (_clipLeft * Scale, _clipTop * Scale, _clipRight * Scale, _clipBottom * Scale);
        var (dx, dy, dw, dh) = (x * Scale, y * Scale, width * Scale, height * Scale);
        var (sx, sy, sw, sh) = (srcX * per, srcY * per, srcWidth * per, srcHeight * per);
        for (var row = 0; row < dh; row++)
        {
            var py = dy + row;
            if (py < top || py >= bottom)
            {
                continue;
            }

            var srow = sy + (int)((long)row * sh / dh);
            if (srow < 0 || srow >= source.Height)
            {
                continue;
            }

            // A tiled band: across, the span repeats at its period from its start.
            var period = 0;
            if (tiled is not null && width != srcWidth)
            {
                foreach (var (bandTop, bandBottom, p) in tiled)
                {
                    if (srow / per >= bandTop && srow / per < bandBottom)
                    {
                        period = p * per;
                        break;
                    }
                }
            }

            var line = srow * source.Width;
            for (var col = 0; col < dw; col++)
            {
                var px = dx + col;
                if (px < left || px >= right)
                {
                    continue;
                }

                var scol = sx + (period > 0 ? (int)((long)col * per / Scale) % period : (int)((long)col * sw / dw));
                if (mirror)
                {
                    scol = sx + sw - 1 - (scol - sx);
                }

                if (scol < 0 || scol >= source.Width)
                {
                    continue;
                }

                var p = source.Pixels[line + scol];
                if (p != Sprite.TransparentIndex)
                {
                    Pixels[(py * PixelWidth) + px] = p;
                }
            }
        }
    }

    /// <summary>
    /// Puts a block of indices made at the frame's own pixels (a generated
    /// sky), its top left at (x, y) in the original's coordinates, clipped.
    /// </summary>
    public void DrawNative(byte[] pixels, int width, int height, int x, int y)
    {
        var (left, top, right, bottom) = (_clipLeft * Scale, _clipTop * Scale, _clipRight * Scale, _clipBottom * Scale);
        var (dx, dy) = (x * Scale, y * Scale);
        for (var row = Math.Max(0, top - dy); row < height && dy + row < bottom; row++)
        {
            var from = Math.Max(0, left - dx);
            var to = Math.Min(width, right - dx);
            if (to > from)
            {
                Array.Copy(pixels, (row * width) + from, Pixels, ((dy + row) * PixelWidth) + dx + from, to - from);
            }
        }
    }

    /// <summary>The picture to draw a sprite from at this scale, and its pixels per sprite pixel: the upscaled one when the scale is even.</summary>
    private (Sprite Source, int PixelsPer) Source(Sprite sprite) =>
        Scale % 2 == 0 && sprite.Hd is { } hd && hd.Width == sprite.Width * 2 && hd.Height == sprite.Height * 2 ? (hd, 2) : (sprite, 1);

    private void DrawScaled(Sprite sprite, int x, int y, byte? tint)
    {
        var (left, top, right, bottom) = (_clipLeft * Scale, _clipTop * Scale, _clipRight * Scale, _clipBottom * Scale);
        // The upscaled picture at any even scale, each of its pixels Scale / 2 wide.
        var (source, per) = Source(sprite);
        var step = Scale / per;
        var px = x * Scale;
        var py = y * Scale;
        for (var row = 0; row < source.Height * step; row++)
        {
            var sy = py + row;
            if (sy < top || sy >= bottom)
            {
                continue;
            }

            var line = sy * PixelWidth;
            var srcLine = row / step * source.Width;
            for (var col = 0; col < source.Width * step; col++)
            {
                var sx = px + col;
                var p = source.Pixels[srcLine + col / step];
                if (sx >= left && sx < right && p != Sprite.TransparentIndex)
                {
                    Pixels[line + sx] = tint ?? p;
                }
            }
        }
    }

    /// <summary>Expands the indices to RGBA bytes (R, G, B, A in memory).</summary>
    public void ToRgba(Palette palette, byte[] rgba)
    {
        var colors = palette.Colors;
        for (var i = 0; i < Pixels.Length; i++)
        {
            var c = colors[Pixels[i]];
            rgba[i * 4] = c.R;
            rgba[i * 4 + 1] = c.G;
            rgba[i * 4 + 2] = c.B;
            rgba[i * 4 + 3] = 255;
        }
    }
}
