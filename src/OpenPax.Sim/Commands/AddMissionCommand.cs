namespace OpenPax.Sim.Commands;

/// <summary>A spy mission started.</summary>
public sealed record AddMissionCommand(int Player, int Target, int Kind, int Level) : Command(Player);
