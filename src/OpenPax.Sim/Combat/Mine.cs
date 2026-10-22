using OpenPax.Sim.Players;

namespace OpenPax.Sim.Combat;

/// <summary>
/// A mine laid round a colony with a minefield (0xB0 bytes). It
/// creeps toward an enemy ship in its sensor range and back home when it
/// loses it, and blows up on touching a ship.
/// </summary>
public sealed class Mine : BattleObject
{
    /// <summary>Its size (+0x8D): shots land within it, and it needs that much free room to be laid.</summary>
    public const int Size = 20;

    public const double TopSpeed = 1.25;

    internal Mine(int owner, Structure field, int level)
    {
        Owner = owner;
        Field = field;
        Damage = field.Tech.InstallationStrength;
        HitPoints = field.Tech.InstallationStrength;
        Level = level;
        SensorRange = field.Tech.InstallationLevel;
    }

    public int Owner { get; }

    /// <summary>The minefield it came from (+0x88), which loses hit points with each mine lost.</summary>
    internal Structure Field { get; }

    /// <summary>Damage done to a ship it touches (+0x6C).</summary>
    public int Damage { get; }

    /// <summary>Hit points (+0x70).</summary>
    public int HitPoints { get; internal set; }

    /// <summary>The minefield's level (+0x74): enemies see the mines with a minesweeper of that level or better.</summary>
    public int Level { get; }

    /// <summary>How far it senses ships (+0xA0).</summary>
    public double SensorRange { get; }

    /// <summary>Where it was laid (+0x90, +0x94), and where it rests.</summary>
    public int HomeX { get; internal set; }

    public int HomeY { get; internal set; }

    /// <summary>The ship it creeps toward (+0x98).</summary>
    internal BattleObject? Target { get; set; }
}
