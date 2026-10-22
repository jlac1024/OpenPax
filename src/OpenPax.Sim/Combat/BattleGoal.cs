namespace OpenPax.Sim.Combat;

/// <summary>
/// Where a ship in a battle is heading (0x20 bytes, vtable): a
/// point, or an object followed wherever it goes.
/// </summary>
public sealed class BattleGoal
{
    private BattleGoal(double x, double y, BattleObject? target)
    {
        X = x;
        Y = y;
        Target = target;
    }

    /// <summary>The point (+0x08, +0x10); a detour has it rewritten in place.</summary>
    public double X { get; internal set; }

    public double Y { get; internal set; }

    /// <summary>The object followed (+0x18), or null for a point.</summary>
    public BattleObject? Target { get; }

    /// <summary>Where it is now: the followed object's exact position, or the point.</summary>
    public (double X, double Y) Point => Target is { } t ? (t.PosX, t.PosY) : (X, Y);

    public static BattleGoal At(int x, int y) => new(x, y, null);

    public static BattleGoal At(double x, double y) => new(x, y, null);

    public static BattleGoal Following(BattleObject target) => new(0.0, 0.0, target);
}
