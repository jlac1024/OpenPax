namespace OpenPax.Sim.Combat;

/// <summary>
/// An explosion or flash on the field (0x90 bytes). It keeps
/// the battle from ending until it has died down. One that follows an
/// object rides along at a fixed offset and vanishes when the object dies;
/// one that does not drifts along its heading.
/// </summary>
public sealed class Effect(int kind, int life) : BattleObject
{
    /// <summary>Which picture sequence it shows (+0x68).</summary>
    public int Kind { get; } = kind;

    /// <summary>Post-steps since it was made (+0x6C); it goes when this reaches <see cref="Life"/>.</summary>
    public int Age { get; internal set; }

    /// <summary>Steps it lasts (the table).</summary>
    public int Life { get; } = life;

    /// <summary>What it rides on (+0x80), or null.</summary>
    public BattleObject? Follow { get; internal set; }

    /// <summary>Its offset from what it follows (+0x84, +0x88).</summary>
    public int OffsetX { get; internal set; }

    public int OffsetY { get; internal set; }
}
