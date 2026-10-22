namespace OpenPax.Render;

/// <summary>
/// Where the original screens' picture sits in a window: the rectangle it
/// fills (the largest whole multiple of its size that fits, centered) and the whole-number scale
/// the forms use for the same window (the original's pixels per window pixel, at least 1).
/// </summary>
public readonly record struct ClassicPlacement(int X, int Y, int Width, int Height, int Scale)
{
    /// <param name="width">The original screens' size in their own coordinates (640).</param>
    public static ClassicPlacement For(int outputWidth, int outputHeight, int width, int height)
    {
        // A whole multiple when it fits, so every pixel lands on the window's
        // own (docs/ui/widescreen.md); a window smaller than the picture gets
        // it shrunk to fit, keeping its shape.
        var scale = Math.Min(outputWidth / width, outputHeight / height);
        if (scale >= 1)
        {
            var (sw, sh) = (width * scale, height * scale);
            return new ClassicPlacement((outputWidth - sw) / 2, (outputHeight - sh) / 2, sw, sh, scale);
        }

        var w = Math.Min(outputWidth, outputHeight * width / height);
        var h = w * height / width;
        return new ClassicPlacement((outputWidth - w) / 2, (outputHeight - h) / 2, w, h, 1);
    }

    /// <summary>A window pixel in the original's coordinates.</summary>
    public (int X, int Y) ToClassic(int x, int y, int width, int height) =>
        ((int)Math.Floor((double)(x - X) * width / Math.Max(1, Width)), (int)Math.Floor((double)(y - Y) * height / Math.Max(1, Height)));
}
