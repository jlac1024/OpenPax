namespace OpenPax.Sim.Commands;

/// <summary>Ships split off their fleet into a new one.</summary>
public sealed record SplitFleetCommand(int Player, int Fleet, int[] Ships) : Command(Player);
