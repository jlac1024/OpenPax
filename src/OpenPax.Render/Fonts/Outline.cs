namespace OpenPax.Render.Fonts;

/// <summary>Coverage grown by a radius in every direction (round for radii above 1): the ring drawn under outlined text.</summary>
public static class Outline
{
    public static byte[] Grow(byte[] coverage, int width, int height, int radius)
    {
        var w = width + radius * 2;
        var h = height + radius * 2;
        var grown = new byte[w * h];
        for (var y = 0; y < h; y++)
        {
            for (var x = 0; x < w; x++)
            {
                var best = 0;
                for (var dy = -radius; dy <= radius && best < 255; dy++)
                {
                    var sy = y - radius + dy;
                    if (sy < 0 || sy >= height)
                    {
                        continue;
                    }

                    for (var dx = -radius; dx <= radius; dx++)
                    {
                        var sx = x - radius + dx;
                        if (sx >= 0 && sx < width && dx * dx + dy * dy <= radius * radius + 1)
                        {
                            best = Math.Max(best, coverage[sy * width + sx]);
                        }
                    }
                }

                grown[y * w + x] = (byte)best;
            }
        }

        return grown;
    }
}
