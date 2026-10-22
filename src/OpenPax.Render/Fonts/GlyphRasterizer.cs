namespace OpenPax.Render.Fonts;

/// <summary>
/// Fills outlines with anti-aliased coverage: each edge adds its signed
/// area to the pixels it crosses, and a running sum along each row gives
/// how much of every pixel is inside (non-zero winding).
/// </summary>
public static class GlyphRasterizer
{
    /// <summary>Coverage 0 to 255 for a w x h box; points are in pixels, y down.</summary>
    public static byte[] Fill(IEnumerable<IReadOnlyList<(float X, float Y)>> contours, int w, int h)
    {
        var area = new float[w * h + 4];
        foreach (var contour in contours)
        {
            for (var i = 0; i < contour.Count; i++)
            {
                Line(area, w, h, contour[i], contour[(i + 1) % contour.Count]);
            }
        }

        var coverage = new byte[w * h];
        for (var row = 0; row < h; row++)
        {
            var sum = 0f;
            for (var col = 0; col < w; col++)
            {
                sum += area[row * w + col];
                coverage[row * w + col] = (byte)Math.Min(255, (int)(Math.Abs(sum) * 255 + 0.5f));
            }
        }

        return coverage;
    }

    private static void Line(float[] a, int w, int h, (float X, float Y) p0, (float X, float Y) p1)
    {
        if (Math.Abs(p0.Y - p1.Y) <= float.Epsilon)
        {
            return;
        }

        var dir = 1f;
        if (p0.Y > p1.Y)
        {
            (p0, p1) = (p1, p0);
            dir = -1f;
        }

        var dxdy = (p1.X - p0.X) / (p1.Y - p0.Y);
        var x = p0.X;
        if (p0.Y < 0)
        {
            x -= p0.Y * dxdy;
        }

        for (var y = Math.Max(0, (int)p0.Y); y < Math.Min(h, (int)Math.Ceiling(p1.Y)); y++)
        {
            var line = y * w;
            var dy = Math.Min(y + 1, p1.Y) - Math.Max(y, p0.Y);
            var xnext = x + dxdy * dy;
            var d = dy * dir;
            var (x0, x1) = x < xnext ? (x, xnext) : (xnext, x);
            var x0f = MathF.Floor(x0);
            var x0i = (int)x0f;
            var x1i = (int)MathF.Ceiling(x1);
            if (x0i < 0 || x1i >= w + 1)
            {
                // Outside the box: clamp into it so the row's total still comes out right.
                x0i = Math.Clamp(x0i, 0, w - 1);
                x1i = Math.Clamp(x1i, 0, w);
            }

            if (x1i <= x0i + 1)
            {
                var xm = 0.5f * (x + xnext) - x0f;
                a[line + x0i] += d - d * xm;
                a[line + x0i + 1] += d * xm;
            }
            else
            {
                var s = 1f / (x1 - x0);
                var fx0 = x0 - x0f;
                var a0 = 0.5f * s * (1 - fx0) * (1 - fx0);
                var fx1 = x1 - MathF.Ceiling(x1) + 1;
                var am = 0.5f * s * fx1 * fx1;
                a[line + x0i] += d * a0;
                if (x1i == x0i + 2)
                {
                    a[line + x0i + 1] += d * (1 - a0 - am);
                }
                else
                {
                    var a1 = s * (1.5f - fx0);
                    a[line + x0i + 1] += d * (a1 - a0);
                    for (var xi = x0i + 2; xi < x1i - 1; xi++)
                    {
                        a[line + xi] += d * s;
                    }

                    var a2 = a1 + (x1i - x0i - 3) * s;
                    a[line + x1i - 1] += d * (1 - a2 - am);
                }

                a[line + x1i] += d * am;
            }

            x = xnext;
        }
    }
}
