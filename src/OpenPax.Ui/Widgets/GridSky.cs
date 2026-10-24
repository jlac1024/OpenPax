using OpenPax.Data.Art;
using OpenPax.Render;

namespace OpenPax.Ui.Widgets;

/// <summary>
/// The port's own background for a panel grown past its art
/// (docs/ui/widescreen.md): a dim starfield and nebula made to the panel's
/// size, so nothing repeats, under the art's grid at its own lines and
/// color. Painted once for a size and palette and kept.
/// </summary>
public sealed class GridSky(uint seed, int firstLineX, int firstLineY, int period, Rgb lineColor)
{
    // Dim enough that the panel's text stays the brightest thing in it.
    private const float Light = 0.55f;

    private (int Width, int Height, Palette Palette, byte[] Pixels)? _painted;

    /// <summary>Draws it over the rectangle; the grid's first lines are at the given offsets inside it.</summary>
    public void Draw(Framebuffer target, int left, int top, int width, int height)
    {
        if (target.Palette is not { } palette || width <= 0 || height <= 0)
        {
            return;
        }

        if (_painted is not { } painted || painted.Width != width || painted.Height != height || !ReferenceEquals(painted.Palette, palette))
        {
            _painted = painted = (width, height, palette, Paint(width, height, palette));
        }

        target.DrawNative(painted.Pixels, width, height, left, top);
    }

    private byte[] Paint(int width, int height, Palette palette)
    {
        var rgb = SystemSky.PaintLight(seed, width, height, nebulaShare: 1f, Light);
        var (r, g, b) = (lineColor.R / 255f, lineColor.G / 255f, lineColor.B / 255f);
        for (var y = 0; y < height; y++)
        {
            for (var x = 0; x < width; x++)
            {
                if (Mod(x - firstLineX, period) == 0 || Mod(y - firstLineY, period) == 0)
                {
                    // The grid lies over the sky, as in the art, but a star or the nebula still shows through a little.
                    var i = ((y * width) + x) * 3;
                    rgb[i] = Math.Max(r, rgb[i] * 0.5f);
                    rgb[i + 1] = Math.Max(g, rgb[i + 1] * 0.5f);
                    rgb[i + 2] = Math.Max(b, rgb[i + 2] * 0.5f);
                }
            }
        }

        return SystemSky.Quantize(rgb, width, height, palette);
    }

    private static int Mod(int value, int period) => ((value % period) + period) % period;
}
