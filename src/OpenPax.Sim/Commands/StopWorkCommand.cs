namespace OpenPax.Sim.Commands;

/// <summary>A colony stops what it is building.</summary>
public sealed record StopWorkCommand(int Player, int Planet) : Command(Player);
