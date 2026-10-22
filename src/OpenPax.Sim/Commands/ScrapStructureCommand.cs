namespace OpenPax.Sim.Commands;

/// <summary>A colony scraps a structure of a kind.</summary>
public sealed record ScrapStructureCommand(int Player, int Planet, int Kind) : Command(Player);
