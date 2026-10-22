using OpenPax.Sim.Numerics;

namespace OpenPax.Sim.Combat;

/// <summary>Something in a battle (combat object base).</summary>
public abstract class BattleObject
{
    /// <summary>
    /// Its number (+0x1C), from one counter for every object of every
    /// battle; the original never resets it. Orders name objects by it, and
    /// the grid holds it in a footprint's corner cell. Never 0.
    /// </summary>
    public int Id { get; internal set; }

    /// <summary>Position on the 3600 by 3600 battlefield, rounded (+0x24, +0x28).</summary>
    public int X { get; internal set; }

    public int Y { get; internal set; }

    /// <summary>The exact position (+0x30, +0x38).</summary>
    public double PosX { get; private set; }

    public double PosY { get; private set; }

    /// <summary>Distance moved per step (+0x40).</summary>
    public double Speed { get; internal set; }

    /// <summary>Heading in whole degrees, 0 to 359 (+0x60).</summary>
    public int Facing { get; internal set; }

    /// <summary>Dead, or gone from the field (+0x64).</summary>
    public bool Dead { get; internal set; }

    /// <summary>Post-steps since it died; it is dropped after the third (+0x65).</summary>
    internal int DeathSteps { get; set; }

    /// <summary>Shots that reached it this step, newest first (+0x14), resolved in the hits phase.</summary>
    internal List<Shot> Incoming { get; } = [];

    /// <summary>Shots in flight aimed at it (+0x20). Quirk kept: a shot split on a hit raises it and never lowers it.</summary>
    internal int AimedAt { get; set; }

    /// <summary>Moves it, keeping the rounded position in step.</summary>
    internal void SetPosition(double x, double y)
    {
        PosX = x;
        PosY = y;
        X = X87.ToInt(x - -0.5);
        Y = X87.ToInt(y - -0.5);
    }
}
