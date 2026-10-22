using OpenPax.Sim.Players;

namespace OpenPax.Sim.Commands;

/// <summary>A fleet sent to another star by its wormholes, after the ships named split off.</summary>
public sealed record FleetRouteCommand(int Player, int Fleet, int Star, FleetOrderKind Kind, bool Replace, int[] SplitOff) : Command(Player);
