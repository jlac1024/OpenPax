using OpenPax.Sim.Players;

namespace OpenPax.Sim.Combat;

/// <summary>
/// A flight of three fighter craft from a carrier ship or a fighter base
/// (0xC0 bytes). It waits docked until launched, then circles
/// its target or the friend it guards, firing from close in.
/// </summary>
public sealed class Fighter : BattleObject
{
    public const int DockedState = 0;
    public const int Returning = 1;
    public const int Attacking = 2;
    public const int Escorting = 3;
    public const int Orphan = 4;

    /// <summary>The reload of a weapon slot it lacks: it never fires.</summary>
    internal const int NoWeapon = 9999;

    internal Fighter(int owner, TechInfo tech, BattleObject carrier, Component? bay, Func<int, TechInfo> techById)
    {
        Owner = owner;
        Tech = tech;
        Carrier = carrier;
        Bay = bay;
        TopSpeed = tech.FighterSpeed;
        Weapon1 = tech.FighterWeapon1 == 0 ? null : techById(tech.FighterWeapon1);
        Weapon2 = tech.FighterWeapon2 == 0 ? null : techById(tech.FighterWeapon2);
        Reload1 = Weapon1?.Reload ?? NoWeapon;
        Reload2 = Weapon2?.Reload ?? NoWeapon;
        MaxHitPoints = tech.FighterHitPoints / 3;
        HitPoints = MaxHitPoints;
    }

    /// <summary>The side it fights for (+0x68).</summary>
    public int Owner { get; }

    public TechInfo Tech { get; }

    /// <summary>The ship or base it flies from (+0x6C); null once that is gone.</summary>
    public BattleObject? Carrier { get; internal set; }

    /// <summary>The carrier ship's fighter bay (+0x98), which takes its craft's losses; null for a base.</summary>
    internal Component? Bay { get; }

    public double TopSpeed { get; }

    internal TechInfo? Weapon1 { get; }

    internal TechInfo? Weapon2 { get; }

    /// <summary>Fire phases until each weapon is ready (+0xA8, +0xAC); it starts loading.</summary>
    internal int Reload1 { get; set; }

    internal int Reload2 { get; set; }

    /// <summary>Hit points of the craft in front (+0xB0) and of each craft (+0xB4).</summary>
    public int HitPoints { get; internal set; }

    public int MaxHitPoints { get; }

    /// <summary>Craft left of the three (+0xB8).</summary>
    public int Craft { get; internal set; } = 3;

    /// <summary>Waiting on its carrier (+0xB9).</summary>
    public bool Docked { get; internal set; } = true;

    /// <summary>
    /// Moves left before it aims for itself after leaving a ship (+0xBC): it
    /// flies straight out, then on the ship's heading.
    /// </summary>
    internal int Straight { get; set; }

    /// <summary>Docked (0), returning (1), attacking (2), guarding a friend (3), or chasing with no carrier (4) (+0x7C).</summary>
    public int State { get; internal set; }

    /// <summary>What it attacks or guards (+0x70).</summary>
    public BattleObject? Link { get; internal set; }

    /// <summary>The heading it wants (+0x78); always the one it has.</summary>
    internal int WantedHeading { get; set; }

    /// <summary>The point it flies to on its circle round the target (+0x88, +0x8C); (0, 0) for none.</summary>
    internal int WaypointX { get; set; }

    internal int WaypointY { get; set; }

    /// <summary>Where it is on that circle (+0x90) and which way it goes round (+0x94).</summary>
    internal int WaypointIndex { get; set; }

    internal int WaypointStep { get; set; } = 1;
}
