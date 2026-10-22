using OpenPax.Sim.Players;

namespace OpenPax.Sim.Combat;

/// <summary>A structure stack defending its planet in a battle (CombatOrbital).</summary>
public sealed class Orbital(Structure structure, int owner) : BattleObject
{
    public Structure Structure { get; } = structure;

    public int Owner { get; } = owner;

    /// <summary>Its size on the field (+0x9D): its sprite's last row plus 30, as a byte.</summary>
    public int Size { get; internal set; }

    /// <summary>The stack's count when the battle began (+0xA0).</summary>
    public int Count { get; } = structure.Count;

    /// <summary>Enemy ships its sensor follows (+0x80), newest first.</summary>
    internal List<BattleObject> Tracked { get; } = [];

    /// <summary>How many it can follow (+0x94): its sensor's targets, 0 with no sensor.</summary>
    public int TrackLimit { get; internal set; }

    /// <summary>The target it was ordered onto (+0x84).</summary>
    public BattleObject? Ordered { get; internal set; }

    /// <summary>The last two things it fired its main weapons at (+0x88, +0x8C).</summary>
    internal BattleObject? LastTarget { get; set; }

    internal BattleObject? TargetBefore { get; set; }

    /// <summary>The last order kind that named a target (+0x98); nothing reads it.</summary>
    internal int OrderState { get; set; }

    /// <summary>Launches its fighters by itself (+0xA4).</summary>
    public bool AutoLaunch { get; internal set; } = true;

    /// <summary>Fire phases until it may launch again (+0xA8).</summary>
    internal int LaunchCountdown { get; set; } = 8;

    /// <summary>Damage per hit: fighters times count plus the last fitted weapon's first band times count (quirk: the second weapon replaces the first).</summary>
    public int Damage(Func<int, TechInfo> techById)
    {
        var s = Structure;
        var weapon = 0;
        if (s.WeaponTechs[0] is { } first)
        {
            weapon = Count * first.Damage[0];
        }

        if (s.WeaponTechs[1] is { } second)
        {
            weapon = Count * second.Damage[0];
        }

        var fighters = 0;
        foreach (var f in s.FighterTechs.OfType<TechInfo>())
        {
            foreach (var id in (int[])[f.FighterWeapon1, f.FighterWeapon2])
            {
                if (id != 0)
                {
                    fighters += techById(id).Damage.Sum();
                }
            }
        }

        return Count * fighters + weapon;
    }
}
