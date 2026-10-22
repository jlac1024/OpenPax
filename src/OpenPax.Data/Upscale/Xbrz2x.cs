namespace OpenPax.Data.Upscale;

/// <summary>
/// Doubles a picture's size the way the xBRZ pixel-art scaler does
/// (docs/formats/upscaled-art.md), in our own code: each 2x2 meeting of
/// pixels is checked for an edge running through it from the colors
/// around it, and the output's four pixels per input pixel are blended
/// towards a neighbor along shallow, steep or diagonal lines, or rounded
/// at a corner. Colors are 0xAABBGGRR (bytes R, G, B, A); clear pixels
/// stay clear and edges against them stay sharp.
/// </summary>
public static class Xbrz2x
{
    private const double EqualColorTolerance = 30;
    private const double DominantDirectionThreshold = 3.6;
    private const double SteepDirectionThreshold = 2.2;
    private const double CenterWeight = 4;

    private const byte None = 0;
    private const byte Normal = 1;
    private const byte Dominant = 2;

    public static uint[] Scale(uint[] source, int width, int height)
    {
        var blend = Corners(source, width, height);
        var output = new uint[width * 2 * height * 2];
        for (var y = 0; y < height; y++)
        {
            for (var x = 0; x < width; x++)
            {
                var e = source[y * width + x];
                var o = y * 2 * width * 2 + x * 2;
                output[o] = output[o + 1] = output[o + width * 2] = output[o + width * 2 + 1] = e;
                for (var k = 0; k < 4; k++)
                {
                    ScaleCorner(source, width, height, blend, output, x, y, k);
                }
            }
        }

        return output;
    }

    /// <summary>
    /// Each pixel's four corners: none, normal or dominant blending,
    /// found from every 2x2 block it is part of. Index: pixel * 4 + corner,
    /// corners numbered by <see cref="CornerIndex"/>.
    /// </summary>
    private static byte[] Corners(uint[] s, int w, int h)
    {
        var blend = new byte[w * h * 4];
        for (var y = -1; y < h; y++)
        {
            for (var x = -1; x < w; x++)
            {
                uint P(int dx, int dy) => At(s, w, h, x + dx, y + dy);
                var (b, c) = (P(0, -1), P(1, -1));
                var (e, f, g, hh) = (P(-1, 0), P(0, 0), P(1, 0), P(2, 0));
                var (i, j, k, l) = (P(-1, 1), P(0, 1), P(1, 1), P(2, 1));
                var (n, o) = (P(0, 2), P(1, 2));
                if ((f == g && j == k) || (f == j && g == k))
                {
                    continue;
                }

                var jg = Dist(i, f) + Dist(f, c) + Dist(n, k) + Dist(k, hh) + CenterWeight * Dist(j, g);
                var fk = Dist(e, j) + Dist(j, o) + Dist(b, g) + Dist(g, l) + CenterWeight * Dist(f, k);
                if (jg < fk)
                {
                    var how = DominantDirectionThreshold * jg < fk ? Dominant : Normal;
                    if (f != g && f != j)
                    {
                        Mark(blend, w, h, x, y, 1, 1, how);
                    }

                    if (k != j && k != g)
                    {
                        Mark(blend, w, h, x + 1, y + 1, -1, -1, how);
                    }
                }
                else if (fk < jg)
                {
                    var how = DominantDirectionThreshold * fk < jg ? Dominant : Normal;
                    if (j != f && j != k)
                    {
                        Mark(blend, w, h, x, y + 1, 1, -1, how);
                    }

                    if (g != f && g != k)
                    {
                        Mark(blend, w, h, x + 1, y, -1, 1, how);
                    }
                }
            }
        }

        return blend;
    }

    private static void Mark(byte[] blend, int w, int h, int x, int y, int sx, int sy, byte how)
    {
        if (x >= 0 && y >= 0 && x < w && y < h)
        {
            ref var b = ref blend[(y * w + x) * 4 + CornerIndex(sx, sy)];
            b = Math.Max(b, how);
        }
    }

    private static int CornerIndex(int sx, int sy) => (sx > 0 ? 1 : 0) + (sy > 0 ? 2 : 0);

    /// <summary>
    /// One corner of one pixel, seen in a frame turned k quarter turns
    /// clockwise so that the corner is always the bottom right: the
    /// neighbors a to i are read through the turn, and so are the output
    /// pixels written.
    /// </summary>
    private static void ScaleCorner(uint[] s, int w, int h, byte[] blend, uint[] output, int x, int y, int k)
    {
        (int X, int Y) Turn(int dx, int dy)
        {
            for (var t = 0; t < k; t++)
            {
                (dx, dy) = (-dy, dx);
            }

            return (dx, dy);
        }

        byte Corner(int sx, int sy)
        {
            var (tx, ty) = Turn(sx, sy);
            return blend[(y * w + x) * 4 + CornerIndex(tx, ty)];
        }

        var bottomRight = Corner(1, 1);
        if (bottomRight == None)
        {
            return;
        }

        uint P(int dx, int dy)
        {
            var (tx, ty) = Turn(dx, dy);
            return At(s, w, h, x + tx, y + ty);
        }

        var (b, c) = (P(0, -1), P(1, -1));
        var (d, e, f) = (P(-1, 0), P(0, 0), P(1, 0));
        var (g, hh, i) = (P(-1, 1), P(0, 1), P(1, 1));

        bool LineBlend()
        {
            if (bottomRight >= Dominant)
            {
                return true;
            }

            // No second blend in a neighboring turn for this pixel (lone pixels stay whole).
            if (Corner(1, -1) != None && !Eq(e, g))
            {
                return false;
            }

            if (Corner(-1, 1) != None && !Eq(e, c))
            {
                return false;
            }

            // An L shape gets only its corner rounded.
            return !(!Eq(e, i) && Eq(g, hh) && Eq(hh, i) && Eq(i, f) && Eq(f, c));
        }

        // The neighbor most like the pixel is what it blends towards.
        var px = Dist(e, f) <= Dist(e, hh) ? f : hh;

        void Blend(int col, int row, int m, int n)
        {
            // The output pixel at (col, row) of the turned 2x2 block.
            var (tx, ty) = Turn(col * 2 - 1, row * 2 - 1);
            var ox = x * 2 + (tx + 1) / 2;
            var oy = y * 2 + (ty + 1) / 2;
            ref var dst = ref output[oy * w * 2 + ox];
            dst = Mix(px, dst, m, n);
        }

        if (!LineBlend())
        {
            // A rounded corner: 1 - pi/4 of the corner pixel.
            Blend(1, 1, 21, 100);
            return;
        }

        var fg = Dist(f, g);
        var hc = Dist(hh, c);
        var shallow = SteepDirectionThreshold * fg <= hc && e != g && d != g;
        var steep = SteepDirectionThreshold * hc <= fg && e != c && b != c;
        if (shallow && steep)
        {
            Blend(1, 0, 1, 4);
            Blend(0, 1, 1, 4);
            Blend(1, 1, 5, 6);
        }
        else if (shallow)
        {
            Blend(0, 1, 1, 4);
            Blend(1, 1, 3, 4);
        }
        else if (steep)
        {
            Blend(1, 0, 1, 4);
            Blend(1, 1, 3, 4);
        }
        else
        {
            Blend(1, 1, 1, 2);
        }
    }

    private static uint At(uint[] s, int w, int h, int x, int y) => s[Math.Clamp(y, 0, h - 1) * w + Math.Clamp(x, 0, w - 1)];

    private static bool Eq(uint a, uint b) => Dist(a, b) < EqualColorTolerance;

    /// <summary>Color distance in YCbCr, with alpha: clear against solid counts as far apart.</summary>
    private static double Dist(uint p, uint q)
    {
        var a1 = (p >> 24) / 255.0;
        var a2 = (q >> 24) / 255.0;
        var d = Rgb(p, q);
        return a1 < a2 ? a1 * d + 255 * (a2 - a1) : a2 * d + 255 * (a1 - a2);
    }

    private static double Rgb(uint p, uint q)
    {
        double r = (int)(p & 0xFF) - (int)(q & 0xFF);
        double g = (int)((p >> 8) & 0xFF) - (int)((q >> 8) & 0xFF);
        double b = (int)((p >> 16) & 0xFF) - (int)((q >> 16) & 0xFF);
        var y = 0.2126 * r + 0.7152 * g + 0.0722 * b;
        var cb = 0.5 / (1 - 0.0722) * (b - y);
        var cr = 0.5 / (1 - 0.2126) * (r - y);
        return Math.Sqrt(y * y + cb * cb + cr * cr);
    }

    /// <summary>m/n of the front color over the back, each weighted by its alpha.</summary>
    private static uint Mix(uint front, uint back, int m, int n)
    {
        var wf = (int)(front >> 24) * m;
        var wb = (int)(back >> 24) * (n - m);
        var sum = wf + wb;
        if (sum == 0)
        {
            return 0;
        }

        uint Channel(int shift) => (uint)((((front >> shift) & 0xFF) * wf + ((back >> shift) & 0xFF) * wb) / sum);
        return Channel(0) | (Channel(8) << 8) | (Channel(16) << 16) | ((uint)(sum / n) << 24);
    }
}
