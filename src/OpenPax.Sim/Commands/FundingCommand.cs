namespace OpenPax.Sim.Commands;

/// <summary>A colony's funding changes.</summary>
public sealed record FundingCommand(int Player, int Planet, int Value) : Command(Player);
