namespace OpenPax.Sim.Commands;

/// <summary>A colony is abandoned.</summary>
public sealed record AbandonCommand(int Player, int Planet) : Command(Player);
