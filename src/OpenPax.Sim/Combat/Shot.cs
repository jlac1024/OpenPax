using OpenPax.Sim.Players;

namespace OpenPax.Sim.Combat;

/// <summary>
/// A shot in flight (0xB8 bytes): a beam, shell or bomb on a
/// fixed heading at full speed, or a missile that speeds up and homes. One
/// shot carries every round of a weapon stack's volley.
/// </summary>
public sealed class Shot(int owner, TechInfo tech, bool pointDefense) : BattleObject
{
    public const double MissileTurnRate = 5.0;
    public const double Acceleration = 0.5;

    /// <summary>The side that fired it.</summary>
    public int Owner { get; } = owner;

    /// <summary>The weapon (+0x68).</summary>
    public TechInfo Tech { get; } = tech;

    /// <summary>Fired by point defense (+0x6C): such a shot only reaches 350.</summary>
    public bool PointDefense { get; } = pointDefense;

    public bool Tracking => Tech.Tracks;

    /// <summary>Top speed (+0x48).</summary>
    public double TopSpeed { get; } = tech.ShotSpeed;

    /// <summary>Where it heads once it has missed or lost its target (+0x70); a missile's homing goal.</summary>
    public BattleGoal? Goal { get; internal set; }

    /// <summary>What it was fired at (+0x74), or null once it missed.</summary>
    public BattleObject? Target { get; internal set; }

    /// <summary>Distance flown (+0x78): its damage falls off with it.</summary>
    public double Flown { get; internal set; }

    /// <summary>A missile's hit points against point defense (+0x88).</summary>
    public int MissileHitPoints { get; internal set; } = tech.MissileHitPoints;

    /// <summary>The shooter's accuracy (+0xA0).</summary>
    public int Accuracy { get; internal set; }

    /// <summary>Rounds it carries (+0xA4): weapons fired, or missiles left.</summary>
    public int Rounds { get; internal set; }

    /// <summary>The weapon's condition in percent (+0xA8); times the rounds for a planet shot.</summary>
    public int Power { get; internal set; }

    /// <summary>Which way its rounds are spread across its line of flight when drawn, 0 to 7 (+0xB1), fixed when fired.</summary>
    public int SpreadGroup { get; internal set; }

    /// <summary>Missed (+0xB2): it flies on to its goal and dies there.</summary>
    public bool Missed { get; internal set; }

    /// <summary>
    /// Damage per round by distance flown, the weapon's band
    /// times its condition, truncated; 0 past the last band (point defense
    /// past 350), which ends the shot. Quirk kept: a planet shot of several
    /// weapons reads a longer table.
    /// </summary>
    public int RoundDamage()
    {
        var f = Power * 0.01;
        var d = Flown;
        int band;
        if (Power <= 100)
        {
            if (PointDefense && d > 350.0)
            {
                return 0;
            }

            band = d <= 300.0 ? 0 : d <= 500.0 ? 1 : d <= 750.0 ? 2 : d <= 1500.0 ? 3 : -1;
        }
        else
        {
            if (PointDefense && d > 650.0)
            {
                return 0;
            }

            band = d <= 500.0 ? 0 : d <= 1000.0 ? 1 : d <= 1500.0 ? 2 : d <= 2000.0 ? 3 : -1;
        }

        return band < 0 ? 0 : Numerics.X87.ToInt(Tech.Damage[band] * f);
    }
}
