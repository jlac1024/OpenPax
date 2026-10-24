using OpenPax.Render.TrueColor;

namespace OpenPax.Ui;

/// <summary>
/// The port's warning mark (a save whose mods are not all loaded): an orange
/// triangle with an exclamation mark cut out, in the shape of Material's
/// "warning" icon, drawn smooth at any size from its outline, 4x4 samples a pixel.
/// </summary>
internal static class WarningIcon
{
    /// <summary>Material's orange 500.</summary>
    public static readonly Color Orange = Color.Rgb(0xFF, 0x98, 0x00);

    private static readonly Dictionary<int, byte[]> _bySize = [];

    public static void Draw(Canvas canvas, int x, int y, int size)
    {
        if (size < 4)
        {
            return;
        }

        if (!_bySize.TryGetValue(size, out var coverage))
        {
            _bySize[size] = coverage = Paint(size);
        }

        canvas.Coverage(coverage, size, size, x, y, Orange);
    }

    private static byte[] Paint(int size)
    {
        const int Samples = 4;
        var coverage = new byte[size * size];
        for (var py = 0; py < size; py++)
        {
            for (var px = 0; px < size; px++)
            {
                var hits = 0;
                for (var sy = 0; sy < Samples; sy++)
                {
                    for (var sx = 0; sx < Samples; sx++)
                    {
                        var u = (px + ((sx + 0.5) / Samples)) / size;
                        var v = (py + ((sy + 0.5) / Samples)) / size;
                        if (Inside(u, v))
                        {
                            hits++;
                        }
                    }
                }

                coverage[(py * size) + px] = (byte)(hits * 255 / (Samples * Samples));
            }
        }

        return coverage;
    }

    // In the unit square: inside the triangle (apex at the top), and not on the mark's bar or dot.
    private static bool Inside(double u, double v)
    {
        const double Top = 0.06;
        const double Bottom = 0.92;
        if (v < Top || v > Bottom)
        {
            return false;
        }

        var half = (v - Top) / (Bottom - Top) * 0.47;
        if (Math.Abs(u - 0.5) > half)
        {
            return false;
        }

        var onBar = Math.Abs(u - 0.5) < 0.06 && v > 0.36 && v < 0.66;
        var onDot = Math.Abs(u - 0.5) < 0.06 && v > 0.73 && v < 0.84;
        return !onBar && !onDot;
    }
}
