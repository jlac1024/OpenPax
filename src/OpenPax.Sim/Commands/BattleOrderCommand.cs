using OpenPax.Sim.Combat;

namespace OpenPax.Sim.Commands;

/// <summary>A command in a battle.</summary>
public sealed record BattleOrderCommand(int Player, BattleOrder Order) : Command(Player);
