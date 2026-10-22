namespace OpenPax.Sim.Players;

/// <summary>A hull (HULL 2000 + type) as designs read it.</summary>
public sealed record HullInfo(int Type, int Structure, int Capacity, int Cost);
