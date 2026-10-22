namespace OpenPax.Sim.Commands;

/// <summary>A design slot is emptied.</summary>
public sealed record DeleteDesignCommand(int Player, int Slot) : Command(Player);
