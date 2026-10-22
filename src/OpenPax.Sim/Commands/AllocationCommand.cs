namespace OpenPax.Sim.Commands;

/// <summary>A research field's share changes.</summary>
public sealed record AllocationCommand(int Player, int Field, int Value) : Command(Player);
