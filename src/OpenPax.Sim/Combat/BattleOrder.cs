namespace OpenPax.Sim.Combat;

/// <summary>
/// An order given in a battle (message class 12): the battle,
/// the target and orbital by id (-1 for none), a point on the field, and
/// the selected ships by id in the order the selection holds them.
/// </summary>
public sealed record BattleOrder(BattleOrderKind Kind, int BattleId, int TargetId, int OrbitalId, int X, int Y, IReadOnlyList<int> ShipIds);
