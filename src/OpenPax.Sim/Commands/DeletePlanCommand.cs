namespace OpenPax.Sim.Commands;

/// <summary>A colony plan is deleted.</summary>
public sealed record DeletePlanCommand(int Player, int Slot) : Command(Player);
