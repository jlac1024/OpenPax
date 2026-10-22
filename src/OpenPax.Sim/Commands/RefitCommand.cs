namespace OpenPax.Sim.Commands;

/// <summary>A ship is marked for refit to a design.</summary>
public sealed record RefitCommand(int Player, int Ship, int DesignSlot) : Command(Player);
