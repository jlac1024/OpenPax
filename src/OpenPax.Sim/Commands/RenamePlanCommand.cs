namespace OpenPax.Sim.Commands;

/// <summary>A colony plan is renamed.</summary>
public sealed record RenamePlanCommand(int Player, int Slot, string Name) : Command(Player);
