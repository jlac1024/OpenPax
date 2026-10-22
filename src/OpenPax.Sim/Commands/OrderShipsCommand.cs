namespace OpenPax.Sim.Commands;

/// <summary>Ships of a design ordered at a colony's shipyard.</summary>
public sealed record OrderShipsCommand(int Player, int Planet, int DesignSlot, int Count) : Command(Player);
