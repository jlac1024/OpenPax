using OpenPax.Data.Art;

namespace OpenPax.Ui.Widgets;

/// <summary>
/// The port's own sky behind a system (docs/ui/system-sky.md): a starfield
/// and, on some systems, a nebula. (Distant spiral galaxies were tried;
/// they read as the game's wormholes.) Made from a
/// seed taken from the star's name, so a system always shows the same sky.
/// Painted in true color at the frame's own pixels, then put into the
/// screen's palette with an ordered dither. Presentation only: its own
/// random numbers, never the game's.
/// </summary>
public static class SystemSky
{
    // 4x4 Bayer matrix, for the dither.
    private static readonly int[] _bayer = [0, 8, 2, 10, 12, 4, 14, 6, 3, 11, 1, 9, 15, 7, 13, 5];

    /// <summary>A stable seed from a name (FNV-1a), the same on every machine and run.</summary>
    public static uint Seed(string name)
    {
        var h = 2166136261u;
        foreach (var c in name)
        {
            h = (h ^ c) * 16777619u;
        }

        return h == 0 ? 1u : h;
    }

    /// <summary>The sky as palette indices, width by height pixels.</summary>
    public static byte[] Paint(uint seed, int width, int height, Palette palette)
    {
        var rgb = new float[width * height * 3];
        var rng = new Rng(seed);
        var scale = Math.Max(1f, Math.Min(width, height) / 270f);

        if (rng.Next() < 0.55f)
        {
            Nebula(rgb, width, height, ref rng, scale);
        }

        Stars(rgb, width, height, ref rng, scale);

        return Quantize(rgb, width, height, palette);
    }

    private static void Nebula(float[] rgb, int width, int height, ref Rng rng, float scale)
    {
        // Two colors of a cloud, from a few the eye reads as space.
        (float R, float G, float B)[] hues =
        [
            (0.55f, 0.15f, 0.65f), (0.15f, 0.35f, 0.75f), (0.75f, 0.2f, 0.25f),
            (0.1f, 0.55f, 0.55f), (0.7f, 0.45f, 0.15f), (0.35f, 0.15f, 0.7f),
        ];
        var a = hues[rng.Int(hues.Length)];
        var b = hues[rng.Int(hues.Length)];
        var noise = rng.UInt();
        var dust = rng.UInt();
        var feature = (120f + (rng.Next() * 120f)) * scale;
        var strength = 0.35f + (rng.Next() * 0.35f);
        var threshold = 0.42f + (rng.Next() * 0.12f);
        for (var y = 0; y < height; y++)
        {
            for (var x = 0; x < width; x++)
            {
                var n = Fbm(noise, x / feature, y / feature, 5);
                var cloud = Math.Clamp((n - threshold) * 2.6f, 0, 1);
                if (cloud <= 0)
                {
                    continue;
                }

                // Dark lanes of dust through it, and its color turning from one hue to the other.
                var lanes = Math.Clamp((Fbm(dust, x / (feature * 0.5f), y / (feature * 0.5f), 4) - 0.45f) * 3, 0, 1);
                var mix = Fbm(dust ^ 0x9E3779B9u, x / (feature * 1.5f), y / (feature * 1.5f), 3);
                var light = cloud * cloud * strength * (1 - (0.7f * lanes));
                var i = ((y * width) + x) * 3;
                rgb[i] += light * ((a.R * (1 - mix)) + (b.R * mix));
                rgb[i + 1] += light * ((a.G * (1 - mix)) + (b.G * mix));
                rgb[i + 2] += light * ((a.B * (1 - mix)) + (b.B * mix));
            }
        }
    }

    private static void Stars(float[] rgb, int width, int height, ref Rng rng, float scale)
    {
        (float R, float G, float B)[] tints =
        [
            (1f, 1f, 1f), (1f, 1f, 1f), (0.85f, 0.9f, 1f), (0.75f, 0.82f, 1f), (1f, 0.95f, 0.8f), (1f, 0.8f, 0.6f), (1f, 0.6f, 0.55f),
        ];
        var area = width * height;

        // Faint dust of distant stars, then fewer brighter ones, then a handful with a glow.
        for (var k = area / 70; k > 0; k--)
        {
            Dot(rgb, width, height, rng.Int(width), rng.Int(height), 0.08f + (rng.Next() * 0.25f), tints[rng.Int(tints.Length)]);
        }

        for (var k = area / 1500; k > 0; k--)
        {
            Dot(rgb, width, height, rng.Int(width), rng.Int(height), 0.5f + (rng.Next() * 0.5f), tints[rng.Int(tints.Length)]);
        }

        for (var k = (int)(area / (40000 * scale * scale)) + 2; k > 0; k--)
        {
            var (x, y, t) = (rng.Int(width), rng.Int(height), tints[rng.Int(tints.Length)]);
            var radius = (1.5f + (rng.Next() * 2.5f)) * scale;
            Glow(rgb, width, height, x, y, radius, 0.9f, t);
            var spike = (int)(radius * 3);
            for (var d = 1; d <= spike; d++)
            {
                var fade = 0.5f * (1 - ((float)d / (spike + 1)));
                Dot(rgb, width, height, x + d, y, fade, t);
                Dot(rgb, width, height, x - d, y, fade, t);
                Dot(rgb, width, height, x, y + d, fade, t);
                Dot(rgb, width, height, x, y - d, fade, t);
            }
        }
    }

    private static void Dot(float[] rgb, int width, int height, int x, int y, float light, (float R, float G, float B) tint)
    {
        if (x < 0 || y < 0 || x >= width || y >= height)
        {
            return;
        }

        var i = ((y * width) + x) * 3;
        rgb[i] += light * tint.R;
        rgb[i + 1] += light * tint.G;
        rgb[i + 2] += light * tint.B;
    }

    private static void Glow(float[] rgb, int width, int height, int cx, int cy, float radius, float light, (float R, float G, float B) tint)
    {
        var reach = (int)(radius * 2.5f) + 1;
        for (var dy = -reach; dy <= reach; dy++)
        {
            for (var dx = -reach; dx <= reach; dx++)
            {
                var d = MathF.Sqrt((dx * dx) + (dy * dy)) / radius;
                Dot(rgb, width, height, cx + dx, cy + dy, light * MathF.Exp(-d * d * 1.4f), tint);
            }
        }
    }

    /// <summary>Into the palette, with an ordered dither so smooth light does not band; the never-drawn index is avoided.</summary>
    private static byte[] Quantize(float[] rgb, int width, int height, Palette palette)
    {
        var lookup = new Dictionary<int, byte>();
        var pixels = new byte[width * height];
        for (var y = 0; y < height; y++)
        {
            for (var x = 0; x < width; x++)
            {
                var i = (y * width) + x;
                var offset = ((_bayer[((y & 3) * 4) + (x & 3)] / 16f) - 0.47f) * 24;
                int Channel(float v) => Math.Clamp((int)((v * 255) + offset), 0, 255) >> 3;
                var key = (Channel(rgb[i * 3]) << 10) | (Channel(rgb[(i * 3) + 1]) << 5) | Channel(rgb[(i * 3) + 2]);
                if (!lookup.TryGetValue(key, out var index))
                {
                    lookup[key] = index = Nearest(palette, (key >> 10) << 3, ((key >> 5) & 31) << 3, (key & 31) << 3);
                }

                pixels[i] = index;
            }
        }

        return pixels;
    }

    private static byte Nearest(Palette palette, int r, int g, int b)
    {
        var (best, bestDistance) = (0, int.MaxValue);
        for (var i = 0; i < palette.Colors.Count; i++)
        {
            if (i == Render.Sprite.TransparentIndex)
            {
                continue;
            }

            var c = palette.Colors[i];
            var d = ((c.R - r) * (c.R - r) * 3) + ((c.G - g) * (c.G - g) * 4) + ((c.B - b) * (c.B - b) * 2);
            if (d < bestDistance)
            {
                (best, bestDistance) = (i, d);
            }
        }

        return (byte)best;
    }

    /// <summary>
    /// Fractal value noise, 0 to 1. Each layer is turned and moved off the
    /// others, and the point itself is pushed about by a slower noise, so the
    /// noise's square lattice never shows as straight edges.
    /// </summary>
    private static float Fbm(uint seed, float x, float y, int octaves)
    {
        var wx = (Value(seed ^ 0x51ED27u, (x * 0.7f) + 3.1f, (y * 0.7f) + 7.7f) - 0.5f) * 0.9f;
        var wy = (Value(seed ^ 0xA341316Cu, (x * 0.7f) + 11.3f, (y * 0.7f) + 1.9f) - 0.5f) * 0.9f;
        (x, y) = (x + wx, y + wy);
        var (sum, amplitude, total) = (0f, 1f, 0f);
        for (var o = 0; o < octaves; o++)
        {
            var (c, n) = (MathF.Cos(0.6f + o), MathF.Sin(0.6f + o));
            var (rx, ry) = ((x * c) - (y * n) + (o * 17.31f), (x * n) + (y * c) + (o * 5.73f));
            sum += amplitude * Value(seed + ((uint)o * 7919u), rx, ry);
            total += amplitude;
            (x, y, amplitude) = (x * 2.03f, y * 2.03f, amplitude * 0.5f);
        }

        return sum / total;
    }

    private static float Value(uint seed, float x, float y)
    {
        var (x0, y0) = ((int)MathF.Floor(x), (int)MathF.Floor(y));
        var (fx, fy) = (x - x0, y - y0);
        (fx, fy) = (fx * fx * (3 - (2 * fx)), fy * fy * (3 - (2 * fy)));
        var a = Corner(seed, x0, y0);
        var b = Corner(seed, x0 + 1, y0);
        var c = Corner(seed, x0, y0 + 1);
        var d = Corner(seed, x0 + 1, y0 + 1);
        return (a + ((b - a) * fx)) + (((c + ((d - c) * fx)) - (a + ((b - a) * fx))) * fy);
    }

    private static float Corner(uint seed, int x, int y)
    {
        var h = seed ^ ((uint)x * 374761393u) ^ ((uint)y * 668265263u);
        h = (h ^ (h >> 13)) * 1274126177u;
        return (h ^ (h >> 16)) / (float)uint.MaxValue;
    }

    /// <summary>xorshift32: small, fast, and the same everywhere.</summary>
    private struct Rng(uint seed)
    {
        private uint _state = seed == 0 ? 1u : seed;

        public uint UInt()
        {
            _state ^= _state << 13;
            _state ^= _state >> 17;
            _state ^= _state << 5;
            return _state;
        }

        public float Next() => (UInt() & 0xFFFFFF) / (float)0x1000000;

        public int Int(int below) => below <= 0 ? 0 : (int)(UInt() % (uint)below);
    }
}
