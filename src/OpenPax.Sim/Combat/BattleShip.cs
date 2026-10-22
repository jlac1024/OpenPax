using OpenPax.Sim.Players;

namespace OpenPax.Sim.Combat;

/// <summary>A ship in a battle (CombatShip).</summary>
public sealed class BattleShip : BattleObject
{
    public const int Idle = 0;
    public const int Turning = 1;
    public const int Moving = 2;
    public const int Attacking = 3;
    public const int Pursuing = 4;
    public const int Leaving = 8;

    // A ship's handling by hull type and armor class, times the root of its speed.
    private static readonly double[] _handling =
    [
        2.0, 1.75, 1.5, 1.25, 1.0,
        1.75, 1.5, 1.25, 1.0, 0.75,
        1.5, 1.25, 1.0, 0.75, 0.5,
        1.1, 1.0, 0.9, 0.8, 0.7,
        0.6, 0.55, 0.5, 0.45, 0.4,
        0.6, 0.55, 0.5, 0.45, 0.4,
    ];

    public BattleShip(Design ship, Fleet fleet, int owner)
    {
        Ship = ship;
        Fleet = fleet;
        Owner = owner;
        UpdateSpeeds();
        var sensor = ship.Targeting;
        SensorRange = sensor is null ? 0 : sensor.Condition * sensor.Tech.SensorRange / 100;
        TargetsTracked = sensor is null ? 0 : sensor.Condition * sensor.Tech.SensorTargets / 100;
    }

    public Design Ship { get; }

    public Fleet Fleet { get; }

    public int Owner { get; }

    /// <summary>
    /// Order state (+0x9C): idle, facing a heading (1), moving to its goal
    /// (2), facing its target (3, also colony ships), chasing its target to
    /// range (4), leaving the field (8).
    /// </summary>
    public int State { get; internal set; }

    /// <summary>What it shoots at (+0x74), or null.</summary>
    public BattleObject? Target { get; internal set; }

    /// <summary>Hull footprint on the grid ("#-F&lt;UU" by hull type, +0xA2).</summary>
    public int Radius => "#-F<UU"[Ship.Type];

    /// <summary>
    /// Which of its 32 facing pictures it shows, 1 to 32 (part of +0x88).
    /// Set when it is placed, and as it turns only on a big step or near a
    /// picture's middle, so it can lag the heading a little.
    /// </summary>
    public int SpriteFrame { get; private set; } = 1;

    internal void UpdateSpriteFrame()
    {
        var f = Numerics.X87.ToInt((Facing - -5.625) * (32.0 / 360.0) + 0.5) + 1;
        SpriteFrame = f > 32 ? f - 32 : f;
    }

    /// <summary>Full speed (+0x48): ships go from still to this at once.</summary>
    public double TopSpeed { get; private set; }

    /// <summary>The root of the speed times the hull and armor class figure (+0x50); nothing in movement reads it.</summary>
    public double Handling { get; private set; }

    /// <summary>Degrees it can turn in a step (+0x58): 11.25 for the two smallest hulls, half and a third of that for larger.</summary>
    public double TurnRate { get; private set; }

    /// <summary>Sensor range (+0x84), and targets it can track (+0x98), with the sensor's condition.</summary>
    public int SensorRange { get; }

    public int TargetsTracked { get; private set; }

    /// <summary>Enemies its sensors follow, newest first (+0x70); more are picked up every 10 sub-steps.</summary>
    internal List<BattleObject> Tracked { get; } = [];

    /// <summary>Fire phases until it may launch fighters by itself again (+0xA8).</summary>
    internal int LaunchCountdown { get; set; } = 8;

    /// <summary>The heading it was told to take (+0x8C).</summary>
    public int WantedHeading { get; internal set; }

    /// <summary>Where it is going (+0x78), and the way round something in the way (+0x7C).</summary>
    public BattleGoal? Goal { get; internal set; }

    public BattleGoal? Detour { get; internal set; }

    /// <summary>Steps it has been held up (+0xA0), a byte: it wraps after 255.</summary>
    internal int Blocked { get; set; }

    /// <summary>Launches its fighters by itself (+0xA4).</summary>
    public bool AutoLaunch { get; internal set; } = true;

    /// <summary>After a component hit: speeds and targets tracked again; the sensor's range stays.</summary>
    internal void AfterComponentHit()
    {
        UpdateSpeeds();
        var sensor = Ship.Targeting;
        TargetsTracked = sensor is null ? 0 : sensor.Condition * sensor.Tech.SensorTargets / 100;
    }

    /// <summary>Speeds from the ship as it is (again after damage).</summary>
    internal void UpdateSpeeds()
    {
        TopSpeed = Ship.CombatSpeed();
        Handling = Math.Sqrt(TopSpeed) * _handling[Ship.Quality + Ship.Type * 5];
        TurnRate = 11.25 / (Ship.Type / 2 + 1);
    }
}
