namespace OpenPax.Sim.Combat;

/// <summary>
/// A band of a colony's improvements that ships can bomb. Every
/// block damages the same colony; a block is a target and a count of the
/// bands left standing.
/// </summary>
public sealed class PlanetBlock(int index) : BattleObject
{
    /// <summary>A shot lands when it comes within this of the block's center (+0x70).</summary>
    public const int HitRadius = 15;

    /// <summary>Its band, 0 to 9 (+0x6C).</summary>
    public int Index { get; } = index;
}
