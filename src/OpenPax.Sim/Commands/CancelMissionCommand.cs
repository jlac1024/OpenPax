namespace OpenPax.Sim.Commands;

/// <summary>A spy mission called off.</summary>
public sealed record CancelMissionCommand(int Player, int Mission) : Command(Player);
