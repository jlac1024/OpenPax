namespace OpenPax.Sim.Numerics;

/// <summary>
/// The original's x87 floating point operations, reproduced with IEEE
/// doubles so results are identical on every platform (docs/numerics.md).
/// The game runs the FPU at 53-bit precision, under which +, -, *, / and
/// sqrt match IEEE double, but fsin, fcos and fpatan still return 64-bit
/// mantissas; the multiply that follows rounds once to 53 bits. Checked
/// against the real instructions by tools/x87_reference.c.
/// </summary>
public static class X87
{
    /// <summary>The game's value for pi: 3.141593, not pi.</summary>
    public const double GamePi = 3.141593;

    /// <summary>1/180 as the game stores it.</summary>
    public const double InverseOf180 = 0.005555555555555556;

    /// <summary>1/3.141593 as the game stores it.</summary>
    public const double InverseOfGamePi = 0.3183098510851024;

    // pi/2 as three doubles, for exact argument reduction.
    private static readonly DoubleDouble _halfPi = new(1.5707963267948966, 6.123233995736766e-17);
    private const double HalfPiTail = -1.4973849048591698e-33;

    // 1/n! for n = 0..27, built once from exact double-double divisions.
    private static readonly DoubleDouble[] _inverseFactorial = BuildInverseFactorials(27);

    /// <summary>Degrees to radians as the game does it: deg * 3.141593 * (1/180), each step rounded.</summary>
    public static double DegreesToRadians(double degrees)
    {
        return degrees * GamePi * InverseOf180;
    }

    /// <summary>round53(fcos(rad) * m), as fld/fcos/fimul or fmul in the original.</summary>
    public static double CosTimes(double rad, double m)
    {
        return Multiply(RoundTo64Bits(SinCos(rad).Cos), m);
    }

    /// <summary>round53(fsin(rad) * m).</summary>
    public static double SinTimes(double rad, double m)
    {
        return Multiply(RoundTo64Bits(SinCos(rad).Sin), m);
    }

    /// <summary>
    /// Direction from one point to another in degrees, 0 to 360, as the
    /// original's: exact values on the axes, otherwise
    /// fpatan * 180 * (1/3.141593), plus 360 when negative.
    /// </summary>
    public static double DirectionDegrees(int dx, int dy)
    {
        if (dy == 0)
        {
            return dx >= 0 ? 0.0 : 180.0;
        }

        if (dx == 0)
        {
            return dy >= 0 ? 90.0 : 270.0;
        }

        var theta = RoundTo64Bits(Atan2(dy, dx));
        var degrees = Multiply(theta, 180.0) * InverseOfGamePi;
        return degrees < 0 ? degrees + 360.0 : degrees;
    }

    /// <summary>The same for exact positions: the same tests and formula on double differences.</summary>
    public static double DirectionDegrees(double dx, double dy)
    {
        if (dy == 0.0)
        {
            return dx >= 0.0 ? 0.0 : 180.0;
        }

        if (dx == 0.0)
        {
            return dy >= 0.0 ? 90.0 : 270.0;
        }

        var theta = RoundTo64Bits(Atan2(dy, dx));
        var degrees = Multiply(theta, 180.0) * InverseOfGamePi;
        return degrees < 0 ? degrees + 360.0 : degrees;
    }

    /// <summary>
    /// The original's float to int conversion: truncate(v - 1) for negative
    /// v, else truncate(v). Floor for fractions, but -3.0 gives -4, a quirk
    /// the port keeps.
    /// </summary>
    public static int ToInt(double v)
    {
        return (int)(v - (v < 0 ? 1.0 : 0.0));
    }

    /// <summary>Rounds a value to the nearest 64-bit mantissa, ties to even, as x87 holds results.</summary>
    internal static DoubleDouble RoundTo64Bits(DoubleDouble v)
    {
        if (v.Hi == 0)
        {
            return v;
        }

        // Exponent of the true value; Hi can sit one binade too high when
        // it is a power of two and Lo pulls the other way.
        var e = Math.ILogB(v.Hi);
        if (Math.ScaleB(1.0, e) == Math.Abs(v.Hi) && v.Lo != 0 && Math.Sign(v.Lo) != Math.Sign(v.Hi))
        {
            e--;
        }

        // Hi is a multiple of the 64-bit unit, so only Lo needs rounding;
        // Hi / unit is even, so rounding Lo's count to even gives the tie
        // rule for the whole value.
        var unit = Math.ScaleB(1.0, e - 63);
        var lo = Math.Round(v.Lo / unit, MidpointRounding.ToEven) * unit;
        var (s, err) = DoubleDouble.TwoSum(v.Hi, lo);
        return new DoubleDouble(s, err);
    }

    /// <summary>round53(v * m) for a 64-bit value v and a double m with few significant bits.</summary>
    internal static double Multiply(DoubleDouble v, double m)
    {
        var (p, e) = DoubleDouble.TwoProduct(v.Hi, m);
        return p + (e + v.Lo * m);
    }

    internal static (DoubleDouble Sin, DoubleDouble Cos) SinCos(DoubleDouble x)
    {
        // x = k * pi/2 + r with |r| <= pi/4, computed in double-double with
        // a third term of pi/2 so r keeps its precision.
        var k = Math.Round(x.Hi / _halfPi.Hi, MidpointRounding.ToEven);
        var r = x - _halfPi * k - (DoubleDouble)(HalfPiTail * k);
        var (s, c) = SeriesSinCos(r);
        return ((long)k & 3) switch
        {
            0 => (s, c),
            1 => (c, -s),
            2 => (-s, -c),
            _ => (-c, s),
        };
    }

    internal static DoubleDouble Atan2(double y, double x)
    {
        // Start from the platform's atan2, then refine with Newton steps in
        // double-double on f(t) = y cos t - x sin t, so the platform's last
        // bit cannot reach the result.
        DoubleDouble t = Math.Atan2(y, x);
        for (var i = 0; i < 2; i++)
        {
            var (s, c) = SinCos(t);
            var f = c * y - s * x;
            var df = -(s * y) - c * x;
            t -= f / df;
        }

        return t;
    }

    private static (DoubleDouble Sin, DoubleDouble Cos) SeriesSinCos(DoubleDouble r)
    {
        var r2 = r * r;
        DoubleDouble sin = _inverseFactorial[27];
        DoubleDouble cos = _inverseFactorial[26];
        for (var n = 25; n >= 1; n -= 2)
        {
            sin = _inverseFactorial[n] - r2 * sin;
            cos = _inverseFactorial[n - 1] - r2 * cos;
        }

        return (sin * r, cos);
    }

    private static DoubleDouble[] BuildInverseFactorials(int max)
    {
        var table = new DoubleDouble[max + 1];
        DoubleDouble f = 1.0;
        table[0] = 1.0;
        for (var n = 1; n <= max; n++)
        {
            f *= n;
            table[n] = (DoubleDouble)1.0 / f;
        }

        return table;
    }
}
