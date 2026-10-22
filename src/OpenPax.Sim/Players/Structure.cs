namespace OpenPax.Sim.Players;

/// <summary>
/// A stack of one to five structures of a kind at a colony:
/// shipyards, missile bases, battle stations and the like.
/// </summary>
public sealed class Structure(TechInfo tech, Colony colony)
{
    public const int MaxCount = 5;

    public TechInfo Tech { get; internal set; } = tech;

    public Colony Colony { get; } = colony;

    public int Count { get; internal set; } = 1;

    /// <summary>A new structure starts all but unbuilt (+0x38).</summary>
    public double HitPoints { get; internal set; } = 1e-8;

    /// <summary>Count times the tech's hit points.</summary>
    public double MaxHitPoints => (double)(Count * Tech.HitPoints);

    /// <summary>+0xB8: set when a unit is added, cleared when building finishes.</summary>
    public bool UnderWork { get; internal set; }

    /// <summary>Three counters a hit on a missile base or battle station can raise (+0x48, +0x50, +0x58); meaning not yet known.</summary>
    public double[] SubsystemDamage { get; } = new double[3];

    /// <summary>Damage the shield has soaked up this battle (+0x0C); it regains some each step.</summary>
    public double ShieldAbsorbed { get; internal set; }

    /// <summary>Minefields: mines laid out in the last battle (+0x8C).</summary>
    public int MinesDeployed { get; internal set; }

    /// <summary>Fire phases until each of its three weapons is ready (+0x18 to +0x20): the two weapons, then the second energy weapon.</summary>
    internal int[] Reload { get; } = new int[3];

    /// <summary>Shots left in each weapon's volley (+0x24 to +0x2C).</summary>
    internal int[] VolleyLeft { get; } = new int[3];

    /// <summary>Clears the battle state when a battle begins: soaked damage, subsystem damage, reloads; full volleys.</summary>
    // (the shield figures it also copies are recomputed where used)
    internal void ResetForBattle()
    {
        ShieldAbsorbed = 0.0;
        Array.Clear(SubsystemDamage);
        Array.Clear(Reload);
        Array.Fill(VolleyLeft, Count);
    }

    /// <summary>The weapon in a slot: the two weapons, then the second energy weapon.</summary>
    internal TechInfo? WeaponInSlot(int slot) => slot < 2 ? WeaponTechs[slot] : SecondEnergyTech;

    /// <summary>
    /// After a shot from a weapon: its
    /// volley goes down by one, and once spent it refills (three times the
    /// count for the third weapon) and the weapon reloads for half its time.
    /// The shot comes before this, so a volley is one shot more than it
    /// holds.
    /// </summary>
    internal void TakeVolley(int slot)
    {
        if (WeaponInSlot(slot) is not { } tech)
        {
            return;
        }

        if (VolleyLeft[slot] < 1)
        {
            VolleyLeft[slot] = slot == 2 ? Count * 3 : Count;
            Reload[slot] = tech.Reload >> 1;
        }
        else
        {
            Reload[slot] = 0;
            VolleyLeft[slot]--;
        }
    }

    /// <summary>Shield, ECM and sensor fitted (+0x60, +0x64, +0x68), for missile bases, battle stations and fighter bases.</summary>
    public TechInfo? ShieldTech { get; internal set; }

    public TechInfo? EcmTech { get; internal set; }

    public TechInfo? SensorTech { get; internal set; }

    /// <summary>A battle station's second energy weapon slot (+0x80), not read by the strength rating.</summary>
    public TechInfo? SecondEnergyTech { get; internal set; }

    /// <summary>Two weapons (+0x78, +0x7C) and three fighters (+0x6C to +0x74) fitted.</summary>
    public TechInfo?[] WeaponTechs { get; } = new TechInfo?[2];

    public TechInfo?[] FighterTechs { get; } = new TechInfo?[3];

    /// <summary>Shipyards: ships ordered, the next to finish first (+0xA0).</summary>
    public List<Design> Queue { get; } = [];

    /// <summary>Shipyards: work done on the head of the queue (+0xA8) and its cost (+0xB0).</summary>
    public double Progress { get; internal set; }

    public double Total { get; internal set; }

    public double Percent => HitPoints / MaxHitPoints * 100.0;

    public void AddTo(StateHash hash)
    {
        foreach (var t in (TechInfo?[])[ShieldTech, EcmTech, SensorTech, SecondEnergyTech, .. WeaponTechs, .. FighterTechs])
        {
            hash.Add(t?.Id ?? 0);
        }

        hash.Add(ShieldAbsorbed).Add(MinesDeployed);
        hash.Add(Tech.Id).Add(Count).Add(HitPoints).Add(UnderWork ? 1 : 0).Add(Progress).Add(Total).Add(Queue.Count);
        foreach (var ship in Queue)
        {
            ship.AddTo(hash);
        }

        foreach (var d in SubsystemDamage)
        {
            hash.Add(d);
        }
    }
}
