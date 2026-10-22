namespace OpenPax.Sim.Players;

/// <summary>The tech a research field is working on (24 bytes in the tech state).</summary>
public sealed class ResearchSlot
{
    public const int None = -1;

    public int Tech { get; internal set; } = None;

    public int Cost { get; internal set; }

    /// <summary>Whole research points put in.</summary>
    public int Progress { get; internal set; }

    /// <summary>The fraction of a point carried to the next period (+0x10).</summary>
    public double Fraction { get; internal set; }

    public bool Done => Cost <= Progress;

    internal void Clear()
    {
        Tech = None;
        Cost = 0;
        Progress = 0;
        Fraction = 0.0;
    }

    public void AddTo(StateHash hash)
    {
        hash.Add(Tech).Add(Cost).Add(Progress).Add(Fraction);
    }
}
