namespace OpenPax.Sim.Players;

/// <summary>A planetary improvement being built or built at a colony.</summary>
public sealed class Improvement(TechInfo tech)
{
    public TechInfo Tech { get; } = tech;

    /// <summary>Construction put in so far, in hit points (+8).</summary>
    public double Built { get; internal set; }

    /// <summary>The tech's hit points (+0x10).</summary>
    public double Total { get; } = tech.HitPoints;

    public double Percent => Built / Total * 100.0;

    public bool Complete => Percent >= 100.0;
}
