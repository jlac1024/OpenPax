namespace OpenPax.Sim.Numerics;

/// <summary>
/// An unevaluated sum of two doubles, about 106 bits of precision, built
/// only from IEEE double +, - and * so it gives the same bits on every
/// platform. Used to compute x87 transcendental results more precisely
/// than they are then rounded. No fused multiply-add: its availability and
/// fallback differ between platforms.
/// </summary>
public readonly record struct DoubleDouble(double Hi, double Lo)
{
    private const double Splitter = 134217729.0; // 2^27 + 1

    public static implicit operator DoubleDouble(double v) => new(v, 0);

    public static DoubleDouble operator +(DoubleDouble a, DoubleDouble b)
    {
        var (s, e) = TwoSum(a.Hi, b.Hi);
        e += a.Lo + b.Lo;
        return QuickTwoSum(s, e);
    }

    public static DoubleDouble operator -(DoubleDouble a)
    {
        return new DoubleDouble(-a.Hi, -a.Lo);
    }

    public static DoubleDouble operator -(DoubleDouble a, DoubleDouble b)
    {
        return a + -b;
    }

    public static DoubleDouble operator *(DoubleDouble a, DoubleDouble b)
    {
        var (p, e) = TwoProduct(a.Hi, b.Hi);
        e += a.Hi * b.Lo + a.Lo * b.Hi;
        return QuickTwoSum(p, e);
    }

    public static DoubleDouble operator /(DoubleDouble a, DoubleDouble b)
    {
        // Long division: two quotient terms and a correction.
        var q1 = a.Hi / b.Hi;
        var r = a - b * q1;
        var q2 = r.Hi / b.Hi;
        r -= b * q2;
        var q3 = r.Hi / b.Hi;
        return QuickTwoSum(q1, q2) + q3;
    }

    /// <summary>Exact sum: s + e equals a + b.</summary>
    public static (double S, double E) TwoSum(double a, double b)
    {
        var s = a + b;
        var bb = s - a;
        var e = (a - (s - bb)) + (b - bb);
        return (s, e);
    }

    /// <summary>Exact product by Dekker's splitting: p + e equals a * b.</summary>
    public static (double P, double E) TwoProduct(double a, double b)
    {
        var p = a * b;
        var (ah, al) = Split(a);
        var (bh, bl) = Split(b);
        var e = ((ah * bh - p) + ah * bl + al * bh) + al * bl;
        return (p, e);
    }

    private static DoubleDouble QuickTwoSum(double a, double b)
    {
        var s = a + b;
        return new DoubleDouble(s, b - (s - a));
    }

    private static (double Hi, double Lo) Split(double a)
    {
        var t = Splitter * a;
        var hi = t - (t - a);
        return (hi, a - hi);
    }
}
