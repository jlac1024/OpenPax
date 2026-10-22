namespace OpenPax.Sim.Players;

/// <summary>What a player's fleet order asks (the fleet order command's kind, +0xC).</summary>
public enum FleetOrderKind
{
    Move = 1,
    ToggleIntercept = 2,
    Colonize = 3,
    Attack = 4,
}
