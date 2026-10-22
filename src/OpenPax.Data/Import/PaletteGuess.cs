using OpenPax.Data.Art;

namespace OpenPax.Data.Import;

/// <summary>
/// Picks the palette a picture was drawn for. Pictures carry no palette;
/// the original shows each one in a window created with a fixed palette
/// (window constructor), so the right palette is a property of
/// where the picture is used, which the data does not record. This
/// measures it instead: under the right palette neighboring pixels are
/// much more alike than random pairs of pixels, while under a wrong one
/// the indices turn into noise and both look the same.
///
/// Small sprites and one-color masks say little on their own, but
/// pictures with nearby ids are drawn by the same screen, so measurements
/// are pooled per group of ids. The result only decides which palette is
/// embedded in the converted PNG; at runtime the port draws with the
/// window's palette, as the original does.
/// </summary>
public static class PaletteGuess
{
    /// <summary>Pictures whose ids fall in the same block of this size are judged together.</summary>
    public const int GroupSize = 50;

    // Enough pairs per picture to separate right from wrong while keeping
    // the import of all 39,176 pictures fast.
    private const int MaxPairs = 4000;

    // Used when nothing in a group can be measured; palette 5 is the one
    // most windows use.
    private const int DefaultPalette = 5;

    // A picture overrides its group only when its own best palette is
    // this much better than the group's.
    private const double OverrideRatio = 1.5;

    // Below this many pairs a picture's own measurement is not trusted.
    private const int MinPairsToOverride = 50;

    // A close call only matters if the two palettes show the picture
    // differently. Palettes 5 to 12 share 36 entries, and many sprites use
    // only those. Below this share of differently colored pixels the
    // choice is moot.
    private const double LooksAlikeShare = 0.05;

    /// <summary>Per-palette sums of color distance between neighboring and between spread-out pixel pairs.</summary>
    public sealed record Measurement(double[] Near, double[] Far, int Pairs)
    {
        public double Score(int palette)
        {
            return Near[palette] / Math.Max(Far[palette], 1);
        }

        public int Best()
        {
            var best = 0;
            for (var p = 1; p < Near.Length; p++)
            {
                if (Score(p) < Score(best))
                {
                    best = p;
                }
            }

            return best;
        }
    }

    /// <summary>A group's palette and how clearly it won.</summary>
    public sealed record GroupChoice(int Palette, int RunnerUp, double Margin);

    public static Measurement Measure(Picture picture, IReadOnlyList<Palette> palettes)
    {
        var (near, far) = SamplePairs(picture);
        var nearSums = new double[palettes.Count];
        var farSums = new double[palettes.Count];
        for (var p = 0; p < palettes.Count; p++)
        {
            nearSums[p] = Sum(near, palettes[p]);
            farSums[p] = Sum(far, palettes[p]);
        }

        return new Measurement(nearSums, farSums, near.Count);
    }

    public static GroupChoice ChooseForGroup(IReadOnlyList<Measurement> members, int paletteCount)
    {
        var near = new double[paletteCount];
        var far = new double[paletteCount];
        foreach (var m in members)
        {
            for (var p = 0; p < paletteCount; p++)
            {
                near[p] += m.Near[p];
                far[p] += m.Far[p];
            }
        }

        if (far.All(f => f == 0))
        {
            return new GroupChoice(DefaultPalette, DefaultPalette, double.PositiveInfinity);
        }

        var pooled = new Measurement(near, far, members.Sum(m => m.Pairs));
        var best = pooled.Best();
        var second = best == 0 ? 1 : 0;
        for (var p = 0; p < paletteCount; p++)
        {
            if (p != best && pooled.Score(p) < pooled.Score(second))
            {
                second = p;
            }
        }

        return new GroupChoice(best, second, pooled.Score(second) / Math.Max(pooled.Score(best), 1e-9));
    }

    /// <summary>The group's palette, unless this picture clearly measures otherwise.</summary>
    public static int ChooseForPicture(Measurement m, GroupChoice group)
    {
        if (m.Pairs < MinPairsToOverride)
        {
            return group.Palette;
        }

        var own = m.Best();
        return m.Score(group.Palette) / Math.Max(m.Score(own), 1e-9) >= OverrideRatio ? own : group.Palette;
    }

    /// <summary>True when the group's choice was close and the runner-up would show this picture differently.</summary>
    public static bool IsUncertain(Picture picture, int chosen, GroupChoice group, IReadOnlyList<Palette> palettes)
    {
        return chosen == group.Palette && group.Margin < 1.1
            && DifferentShare(picture, palettes[group.Palette], palettes[group.RunnerUp]) >= LooksAlikeShare;
    }

    private static (List<(byte, byte)> Near, List<(byte, byte)> Far) SamplePairs(Picture picture)
    {
        var w = picture.Width;
        var px = picture.Pixels;
        var near = new List<(byte, byte)>();
        var opaque = new List<int>();
        var step = Math.Max(1, px.Length / MaxPairs);
        for (var i = 0; i < px.Length; i += step)
        {
            if (px[i] == Picture.Transparent)
            {
                continue;
            }

            opaque.Add(i);
            var x = i % w;
            if (x + 1 < w && px[i + 1] != Picture.Transparent)
            {
                near.Add((px[i], px[i + 1]));
            }

            if (i + w < px.Length && px[i + w] != Picture.Transparent)
            {
                near.Add((px[i], px[i + w]));
            }
        }

        // Fixed, spread-out pairing instead of random sampling, so every
        // import picks the same palettes.
        var far = new List<(byte, byte)>(near.Count);
        for (var k = 0; k < near.Count && opaque.Count > 1; k++)
        {
            var a = opaque[(int)((long)k * 7919 % opaque.Count)];
            var b = opaque[(int)(((long)k * 104729 + opaque.Count / 2) % opaque.Count)];
            far.Add((px[a], px[b]));
        }

        return (near, far);
    }

    private static double Sum(List<(byte A, byte B)> pairs, Palette palette)
    {
        long total = 0;
        foreach (var (a, b) in pairs)
        {
            var ca = palette[a];
            var cb = palette[b];
            total += Math.Abs(ca.R - cb.R) + Math.Abs(ca.G - cb.G) + Math.Abs(ca.B - cb.B);
        }

        return total;
    }

    private static double DifferentShare(Picture picture, Palette a, Palette b)
    {
        int opaque = 0, different = 0;
        foreach (var index in picture.Pixels)
        {
            if (index == Picture.Transparent)
            {
                continue;
            }

            opaque++;
            if (a[index] != b[index])
            {
                different++;
            }
        }

        return opaque == 0 ? 0 : (double)different / opaque;
    }
}
