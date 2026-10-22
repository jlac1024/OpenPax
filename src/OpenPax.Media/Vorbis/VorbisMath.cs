namespace OpenPax.Media.Vorbis;

/// <summary>Helper functions defined in the Vorbis I spec, section 9.2.</summary>
internal static class VorbisMath
{
    /// <summary>ilog: the position of the highest set bit, 0 for 0 and negative values.</summary>
    public static int ILog(long x)
    {
        var r = 0;
        while (x > 0)
        {
            r++;
            x >>= 1;
        }

        return r;
    }

    /// <summary>float32_unpack: the spec's own 32-bit float encoding.</summary>
    public static float Float32Unpack(uint x)
    {
        var mantissa = (double)(x & 0x1FFFFF);
        var exponent = (int)((x & 0x7FE00000) >> 21);
        if ((x & 0x80000000) != 0)
        {
            mantissa = -mantissa;
        }

        return (float)(mantissa * Math.Pow(2, exponent - 788));
    }

    /// <summary>lookup1_values: the largest r with r^dimensions not above entries.</summary>
    public static int Lookup1Values(int entries, int dimensions)
    {
        var r = (int)Math.Floor(Math.Pow(entries, 1.0 / dimensions));
        // Correct for floating point error either way.
        while (Pow(r + 1, dimensions) <= entries)
        {
            r++;
        }

        while (r > 0 && Pow(r, dimensions) > entries)
        {
            r--;
        }

        return r;
    }

    private static long Pow(long b, int e)
    {
        long v = 1;
        for (var i = 0; i < e; i++)
        {
            v *= b;
            if (v > int.MaxValue)
            {
                return v;
            }
        }

        return v;
    }
}
