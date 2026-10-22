namespace OpenPax.Sim.Commands;

/// <summary>A design goes in the first free slot.</summary>
public sealed record SaveDesignCommand(int Player, DesignSpec Design) : Command(Player);
