namespace OpenPax.Sim.Combat;

/// <summary>
/// The planet fought over, as an object of the battle. It never
/// moves, fires or takes hits; it stands at (0, 0) until a block falls and
/// it takes that block's place, so the block's ruin, which rides on it,
/// shows there.
/// </summary>
public sealed class CombatPlanet : BattleObject
{
}
