using OpenPax.Sim.Galaxy;

namespace OpenPax.Sim.Players;

/// <summary>Where a fleet is told to go: an object, or a point when there is none.</summary>
public sealed class Order(ISpaceObject? target, int x, int y)
{
    public ISpaceObject? Target { get; } = target;

    /// <summary>The target's current position, or the point.</summary>
    public (int X, int Y) Position => Target is { } t ? (t.X, t.Y) : (x, y);

    public static Order To(ISpaceObject target)
    {
        // an object target stores the point -1, -1.
        return new Order(target, -1, -1);
    }

    public static Order ToPoint(int x, int y)
    {
        return new Order(null, x, y);
    }
}
