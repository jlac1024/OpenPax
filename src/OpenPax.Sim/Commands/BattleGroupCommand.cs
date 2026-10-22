namespace OpenPax.Sim.Commands;

/// <summary>Ships put in a battle group, 1 to 9, or 0 for none.</summary>
public sealed record BattleGroupCommand(int Player, int[] Ships, int Group) : Command(Player);
