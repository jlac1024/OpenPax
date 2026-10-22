namespace OpenPax.Sim.Players;

/// <summary>
/// The parts of a technology the simulation reads (see OpenPax.Data TECH
/// records and docs/sim/ships.md). Class fields are 0 for other classes.
/// </summary>
/// <param name="Kind">Role of the tech; for example 5 shipyard, 7 missile base, 24 armor.</param>
public sealed record TechInfo(int Id, int Kind, int ResearchCost)
{
    public const int ColonizerKind = 2;
    public const int MinesweeperKind = 3;
    public const int ShipyardKind = 5;
    public const int MissileBaseKind = 7;
    public const int StarDriveKind = 11;
    public const int TacticalDriveKind = 12;
    public const int SensorKind = 13;
    public const int EcmKind = 14;
    public const int EnergyWeaponKind = 15;
    public const int ProjectileKind = 16;
    public const int MissileKind = 17;
    public const int PlanetBombKind = 18;
    public const int AreaWeaponKind = 19;
    public const int ScreenKind = 20;
    public const int ShieldKind = 21;
    public const int FieldKind = 22;
    public const int FighterKind = 23;
    public const int ArmorKind = 24;

    public const int ImprovementKind = -2;
    public const int TrainingBaseKind = 6;
    public const int BattleStationKind = 8;
    public const int FighterBaseKind = 9;
    public const int MineFieldKind = 10;
    public const int ImprovementClass = 6010;
    public const int SpecialClass = 6009;

    /// <summary>
    /// The port's own, for mods' techs (docs/modding/content.md): the tech
    /// whose place among its kind gives this one's looks and place-based
    /// roles (a fighter's art and base slot, a colonizer's generation).
    /// 0 for the game's own techs, which are their own.
    /// </summary>
    public int Look { get; init; }

    /// <summary>The tech this one looks like: <see cref="Look"/>, or itself.</summary>
    public int LookId => Look == 0 ? Id : Look;

    /// <summary>TECH class 6001 to 6010.</summary>
    public int ClassId { get; init; }

    /// <summary>The first of the tech's 36 animation pictures in Tech.img; its 50x50 icon is the one before.</summary>
    public int Picture { get; init; }

    /// <summary>The line of the research tree it belongs to (STR# 105 names them); 0 for theories.</summary>
    public int ResearchGroup { get; init; }

    /// <summary>Per-component cost, scaled by tech level.</summary>
    public int Cost { get; init; }

    /// <summary>What building one costs in construction (TECH +0x20 in the original's view).</summary>
    public int BuildCost { get; init; }

    /// <summary>Planetary improvements: per-population amount of credits, research, espionage, construction, population.</summary>
    public IReadOnlyList<double> PerPopulation { get; init; } = [0, 0, 0, 0, 0];

    /// <summary>Planetary improvements: flat amount of each resource.</summary>
    public IReadOnlyList<double> Flat { get; init; } = [0, 0, 0, 0, 0];

    /// <summary>
    /// The one resource an improvement works on: the first with a flat
    /// value, else the first with a per-population value, else credits.
    /// </summary>
    public int Resource
    {
        get
        {
            for (var r = 0; r < 5; r++)
            {
                if (Flat[r] != 0.0)
                {
                    return r;
                }
            }

            for (var r = 0; r < 5; r++)
            {
                if (PerPopulation[r] != 0.0)
                {
                    return r;
                }
            }

            return 0;
        }
    }

    public int HitPoints { get; init; }

    /// <summary>Space used in a design, scaled by tech level.</summary>
    public int Size { get; init; }

    /// <summary>Star drives: wormhole jumps away from supply.</summary>
    public int DriveRating { get; init; }

    /// <summary>Star drives: largest hull class moved (2, 3 or 5).</summary>
    public int MaxHullClass { get; init; }

    /// <summary>Tactical drives.</summary>
    public int CombatSpeed { get; init; }

    public int DefenseBonus { get; init; }

    /// <summary>Weapons: damage in the four range bands.</summary>
    public IReadOnlyList<int> Damage { get; init; } = [0, 0, 0, 0];

    public int Reload { get; init; }

    /// <summary>A weapon's accuracy bonus (+0x58), added to its shots' chance to hit.</summary>
    public int WeaponAccuracy { get; init; }

    /// <summary>A missile's hit points against point defense (+0x4C).</summary>
    public int MissileHitPoints { get; init; }

    /// <summary>Weapons: which picture set and sound its shots have in battle (+0x48).</summary>
    public int WeaponEffect { get; init; }

    /// <summary>A shot's top speed per step (+0x64 doubled).</summary>
    public int ShotSpeed { get; init; }

    /// <summary>A weapon's shots home on their target (tracking byte not 255): missiles.</summary>
    public bool Tracks => WeaponFlag != 0xFF;

    /// <summary>Weapons: the byte before the weapon type (255 for non-missiles); battle stations take only weapons with 255. Meaning not yet certain.</summary>
    public int WeaponFlag { get; init; }

    /// <summary>Weapons: 0 normal, 1 planet bomb, 2 area.</summary>
    public int WeaponType { get; init; }

    public int ShieldCapacity { get; init; }

    /// <summary>Shields: capacity regained each combat step.</summary>
    public int ShieldRegeneration { get; init; }

    /// <summary>Targeting sensors: accuracy bonus.</summary>
    public int SensorAccuracy { get; init; }

    /// <summary>A targeting sensor's range (+0x48) and how many targets it tracks (+0x4C).</summary>
    public int SensorRange { get; init; }

    public int SensorTargets { get; init; }

    /// <summary>ECM: strength.</summary>
    public int EcmStrength { get; init; }

    public int ShieldPercent { get; init; }

    /// <summary>Armor: the byte before the multiplier; computer players use a design only if its hull type is at most this. Meaning not yet certain.</summary>
    public int ArmorHullLimit { get; init; }

    /// <summary>Armor: multiplies a design's structure.</summary>
    public double StructureMultiplier { get; init; }

    /// <summary>Fighters: the two weapon tech ids (0 for none).</summary>
    public int FighterWeapon1 { get; init; }

    public int FighterWeapon2 { get; init; }

    /// <summary>Fighters: taken off a beam's chance to hit (+0x4E).</summary>
    public int FighterDefense { get; init; }

    /// <summary>Fighters: hit points of a flight; each of its three craft has a third (+0x50).</summary>
    public int FighterHitPoints { get; init; }

    /// <summary>Fighters: speed in battle (+0x48).</summary>
    public int FighterSpeed { get; init; }

    /// <summary>Fighters: the accuracy of their shots (+0x52).</summary>
    public int FighterAccuracy { get; init; }

    /// <summary>Fighters: taken off a missile's tracking (+0x54).</summary>
    public int FighterMissileDefense { get; init; }

    /// <summary>Installations and specials: level 1 to 5 (also repair amount; a minefield's mine sensor range).</summary>
    public int InstallationLevel { get; init; }

    /// <summary>Installations: 100 or 500, else 0; a minefield's mine damage and hit points.</summary>
    public int InstallationStrength { get; init; }

    /// <summary>Four tech ids needed first; 0 for none.</summary>
    public IReadOnlyList<int> Prerequisites { get; init; } = [0, 0, 0, 0];

    /// <summary>Research field by class: 0 weapons, 1 shielding, 2 ship, 3 colonial, 4 space.</summary>
    public int ResearchField => ClassId switch
    {
        6001 or 6002 or 6003 or 6004 => 2,
        6006 or 6008 => 1,
        6007 or 6009 => 4,
        6010 => 3,
        _ => 0,
    };

    /// <summary>The design slot a component of this tech goes in.</summary>
    public int DesignSlot => ClassId switch
    {
        6001 => 0,
        6002 => 1,
        6003 => 2,
        6004 => 3,
        6008 => 4,
        6006 => 5,
        6005 => 6,
        6007 or 6010 => 7,
        6009 => 8,
        _ => 0,
    };
}
