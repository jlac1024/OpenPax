namespace SimpleCombat;

/// <summary>One side's fire at another in a round, after hit chances and interception: beams and drones meet shields, missiles do not.</summary>
public readonly record struct Volley(double Beam, double Missile, double Drones, double Bombs)
{
    public double Total => Beam + Missile + Drones;

    public static Volley operator +(Volley a, Volley b) => new(a.Beam + b.Beam, a.Missile + b.Missile, a.Drones + b.Drones, a.Bombs + b.Bombs);

    public Volley Times(double f) => new(Beam * f, Missile * f, Drones * f, Bombs * f);
}
