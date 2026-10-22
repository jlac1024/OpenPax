using OpenPax.Sim.Players;

namespace OpenPax.Sim.Commands;

/// <summary>A fleet order: to an object by id (-1 for a point at X, Y), replacing the orders or added after them.</summary>
public sealed record FleetOrderCommand(int Player, int Fleet, FleetOrderKind Kind, int Target, int X, int Y, bool Replace) : Command(Player);
