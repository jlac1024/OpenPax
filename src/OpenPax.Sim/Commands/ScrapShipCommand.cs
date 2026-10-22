namespace OpenPax.Sim.Commands;

/// <summary>A ship is scrapped.</summary>
public sealed record ScrapShipCommand(int Player, int Ship) : Command(Player);
