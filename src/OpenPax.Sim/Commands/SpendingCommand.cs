namespace OpenPax.Sim.Commands;

/// <summary>A spending field changes.</summary>
public sealed record SpendingCommand(int Player, int Field, double Value) : Command(Player);
