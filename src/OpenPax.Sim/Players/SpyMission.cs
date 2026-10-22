namespace OpenPax.Sim.Players;

/// <summary>
/// An espionage mission one player runs against another (0x30 bytes,
/// ). Espionage points go into it until it is paid for; then it
/// is tried once and goes (docs/sim/espionage.md).
/// </summary>
public sealed class SpyMission
{
    public const int StealShips = 0;
    public const int DestroyShips = 1;
    public const int Sabotage = 2;
    public const int LocateColony = 3;
    public const int TurnAdvisors = 4;
    public const int TurnGovernors = 5;
    public const int Assassinate = 6;
    public const int RevealSystems = 7;
    public const int StealTechs = 8;

    public const int Kinds = 9;
    public const int Levels = 3;

    internal SpyMission(int id, Player owner, Player target, int kind, int level)
    {
        Id = id;
        Owner = owner;
        Target = target;
        Kind = kind;
        Level = level;
        Cost = LevelCost(level) * KindCost(kind);
    }

    /// <summary>
    /// What the new mission popup quotes: the cost in espionage
    /// points and the chance of getting in, in whole percent, doubled for a
    /// spy species. The popup keeps its own copy of the same tables.
    /// </summary>
    public static (int Cost, int ChancePercent) Preview(int kind, int level, bool spy)
    {
        var chance = KindChance(kind) * LevelChance(level);
        if (spy)
        {
            chance += chance;
        }

        return ((int)(KindCost(kind) * LevelCost(level)), Numerics.X87.ToInt(chance * 100.0));
    }

    /// <summary>The mission's own number (+0x2C), from the game's counter.</summary>
    public int Id { get; }

    /// <summary>Who runs it (+0xC).</summary>
    public Player Owner { get; }

    /// <summary>Who it is run against (+8).</summary>
    public Player Target { get; }

    /// <summary>One of the nine kinds (+0x10).</summary>
    public int Kind { get; }

    /// <summary>0 to 2: higher costs more and is likelier to work (+0x28).</summary>
    public int Level { get; }

    /// <summary>Espionage points it takes (+0x18).</summary>
    public double Cost { get; }

    /// <summary>Espionage points put in (+0x20).</summary>
    public double Progress { get; internal set; }

    public bool Paid => Progress >= Cost;

    public int Percent => Cost <= 0.0 ? 100 : (int)(Progress / Cost * 100.0);

    /// <summary>Chance of getting in, before the defense roll: 0 to 1 (more for spy species).</summary>
    internal double Chance => LevelChance(Level) * KindChance(Kind);

    // The original keeps each table as (cost, chance) pairs of doubles,
    // kinds and levels.
    private static double KindCost(int kind) => kind switch
    {
        0 or 3 or 4 => 50.0,
        1 or 2 => 25.0,
        5 or 7 => 20.0,
        _ => 30.0,
    };

    private static double KindChance(int kind) => kind switch
    {
        4 or 5 => 0.1,
        0 or 3 or 6 => 0.15,
        1 or 2 or 8 => 0.2,
        _ => 0.25,
    };

    private static double LevelCost(int level) => level switch
    {
        0 => 1.0,
        1 => 2.0,
        _ => 4.0,
    };

    private static double LevelChance(int level) => level switch
    {
        0 => 1.0,
        1 => 1.5,
        _ => 2.0,
    };

    public void AddTo(StateHash hash)
    {
        hash.Add(Id).Add(Owner.Index).Add(Target.Index).Add(Kind).Add(Level).Add(Cost).Add(Progress);
    }
}
