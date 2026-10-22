namespace OpenPax.Sim.Commands;

/// <summary>A colony plan is copied to an empty slot.</summary>
public sealed record CopyPlanCommand(int Player, int From, int To, string Name) : Command(Player);
