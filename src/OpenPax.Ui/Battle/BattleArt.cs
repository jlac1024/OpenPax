using OpenPax.Sim.Combat;
using OpenPax.Sim.Players;

namespace OpenPax.Ui.Battle;

/// <summary>Which Combat.img pictures the battle's objects show (build/scratch-ui/combat-draw.md 2).</summary>
internal static class BattleArt
{
    public const int EngineFrames = 50;
    public const int Mask = 100000;
    public const int HealthBack = 20002;
    public const int HealthFill = 20003;
    public const int MinimapDot = 20000;
    public const int MinimapPlanet = 20510;
    public const int Mine = 19050;
    public const int Frames = 32;

    /// <summary>Each shot effect's first picture and frame count (the table).</summary>
    private static readonly (int First, int Frames)[] _shots =
    [
        (25000, 32), (25050, 32), (25100, 32), (25150, 32), (25200, 32), (25250, 32),
        (27000, 8), (27020, 1), (27040, 1), (27060, 8), (27080, 8), (27100, 4), (27120, 8), (27140, 4),
        (27160, 8), (27180, 5), (27200, 5), (27220, 8), (27240, 8), (27260, 4), (27280, 4), (27300, 4),
        (27320, 4), (27340, 4), (27360, 4), (27380, 4), (27400, 4), (27420, 8), (27440, 4),
        (30000, 32), (30100, 32), (30200, 32), (30300, 32), (30400, 32), (30500, 32),
        (35000, 32), (35100, 32), (35200, 32), (35300, 32), (35400, 32), (35500, 32), (35600, 32),
        (35700, 32), (35800, 32), (35900, 32),
    ];

    /// <summary>Each shot effect's sound (the third column of the table).</summary>
    private static readonly int[] _shotSounds =
    [
        31010, 31010, 31340, 31000, 31000, 31000, 31040, 31040, 31040, 31040, 31040, 31310, 31310, 31310,
        31300, 31300, 31300, 31310, 31330, 31310, 31340, 31340, 31020, 31020, 31020, 31020, 31310, 31350,
        31030, 31200, 31200, 31210, 31210, 31230, 31220, 31200, 31200, 31200, 31210, 31210, 31230, 31230,
        31230, 31220, 31240,
    ];

    private static readonly int[] _explosions = [21000, 21050, 21100, 21200, 21250, 21300, 21400, 21450, 21500];

    /// <summary>The ruins and splits of blocks, kinds 131 to 142 (part of the table).</summary>
    private static readonly int[] _blockEffects =
    [
        22100, 22250, 22400, 22550, 22150, 22300, 22450, 22600, 22200, 22350, 22500, 22650,
    ];

    /// <summary>A ship's picture: its owner's fleet color and hull, its facing frame, engine frames while moving.</summary>
    public static int Ship(BattleShip ship, int fleetColor, bool engines)
    {
        return (fleetColor + 1) * 1000 + ship.Ship.Type * 100 + ship.SpriteFrame + (engines ? EngineFrames : 0);
    }

    /// <summary>An orbital's first picture by kind and count; 0 for a kind with none.</summary>
    public static int Orbital(Orbital orbital)
    {
        var n = orbital.Count;
        return orbital.Structure.Tech.Kind switch
        {
            8 => Math.Min((n + 339) * 50, 17450),
            7 => Math.Min((n + 349) * 50, 17950),
            9 => Math.Min((n + 359) * 50, 18450),
            10 => Math.Min((n + 379) * 50, 19450),
            5 => 19960,
            6 => 19910,
            _ => 0,
        };
    }

    /// <summary>A fighter's first picture by its tech's place among the fighters.</summary>
    public static int Fighter(Fighter fighter, int firstFighterId)
    {
        // A mod's fighter takes the art of the one it is like (TechInfo.LookId).
        var t = fighter.Tech.LookId - firstFighterId;
        return (t % 6 + 1000 + t / 6 * 6) * 50;
    }

    /// <summary>A heading's frame of 32, frame 0 centered on 0 degrees; fighters leave out the half.</summary>
    public static int HeadingFrame(int heading, bool round)
    {
        var f = (int)((heading - -5.625) * (32.0 / 360.0) + (round ? 0.5 : 0.0));
        return f > 31 ? f - 32 : f;
    }

    /// <summary>A shot's picture: animated bolts cycle by draws, the rest show their heading.</summary>
    public static int Shot(Shot shot, int draws)
    {
        var (first, frames) = _shots[Math.Clamp(shot.Tech.WeaponEffect, 0, _shots.Length - 1)];
        return first is >= 27000 and < 30000 ? first + draws % frames : first + HeadingFrame(shot.Facing, true);
    }

    /// <summary>The sound a shot makes as it is fired.</summary>
    public static int ShotSound(Shot shot) => _shotSounds[Math.Clamp(shot.Tech.WeaponEffect, 0, _shotSounds.Length - 1)];

    /// <summary>The sound an effect makes as it first shows (the table); 0 for none.</summary>
    public static int EffectSound(Effect effect) => effect.Kind switch
    {
        <= 8 or (>= 135 and <= 138) or 143 => 28600,
        9 => 28400,
        10 => 28200,
        >= 131 and <= 134 => 30000,
        >= 139 and <= 142 => 30200,
        _ => 0,
    };

    /// <summary>An effect's picture (the table) at its age.</summary>
    public static int Effect(Effect effect)
    {
        var k = effect.Kind;
        var first = k switch
        {
            <= 8 => _explosions[Math.Max(k, 0)],
            9 or 10 or 144 => 21600,
            <= 130 => 40000 + ((k - 11) / 8 / 3 * 1000) + ((k - 11) / 8 % 3 * 100) + ((k - 11) % 8 * 10),
            <= 142 => _blockEffects[k - 131],
            _ => 21650,
        };
        return first + effect.Age;
    }

    /// <summary>The color things of a player show in to the local player: own, at war, on terms, or otherwise.</summary>
    public static byte Relation(IReadOnlyList<Player> players, int local, int owner)
    {
        if (owner == local)
        {
            return 25;
        }

        var level = local < players.Count && owner >= 0 && owner < players.Count ? players[local].Treaties[owner].Level() : Treaty.None;
        return level switch
        {
            Treaty.War => 18,
            >= 1 and <= 4 => 96,
            _ => 115,
        };
    }
}
