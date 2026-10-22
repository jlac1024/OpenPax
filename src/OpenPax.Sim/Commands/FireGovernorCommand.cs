namespace OpenPax.Sim.Commands;

/// <summary>A governor dismissed.</summary>
public sealed record FireGovernorCommand(int Player, int Advisor) : Command(Player);
