namespace OpenPax.Sim.Galaxy;

/// <summary>
/// The star's own object in its system view, at the center (star +0x20, a
/// star sprite). Fleets can be sent to it and orbit it like a planet: this
/// is movement.md's fourth kind of target.
/// </summary>
public sealed class StarCenter(int star) : ISpaceObject
{
    public const int Position = 220;

    public int Star { get; } = star;

    public int X => Position;

    public int Y => Position;
}
