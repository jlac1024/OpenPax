using OpenPax.Sim.Players;

namespace SimpleCombat;

/// <summary>
/// A besieging fleet flying in under a planet's guns. In the original's
/// battles the attackers start far off (some 2,900 from the defenses) and
/// close at their slowest ship's speed; defenses that reach farther than the
/// fleet's own weapons fire all the while it closes, and it cannot answer.
/// Simple Combat has no distances, so each fleet that joins a siege first
/// spends that time as rounds in which it is fired on but does not fire.
/// </summary>
public static class Approach
{
    /// <summary>A weapon's reach, as the original's battles have it: its last range band with damage, 300, 500, 750 or 1500.</summary>
    public static int Reach(TechInfo weapon)
    {
        var d = weapon.Damage;
        return d.Count > 3 && d[3] > 0 ? 1500 : d.Count > 2 && d[2] > 0 ? 750 : d.Count > 1 && d[1] > 0 ? 500 : d.Count > 0 && d[0] > 0 ? 300 : 0;
    }

    /// <summary>
    /// The rounds a fleet spends closing on the orbitals: the reach they have
    /// past its own, over its slowest ship's battle speed, in rounds of
    /// approach_steps_per_round battle steps; none when it reaches as far.
    /// </summary>
    public static int Rounds(IEnumerable<Structure> orbitals, Fleet fleet, Settings settings)
    {
        var guns = orbitals.SelectMany(o => o.WeaponTechs.OfType<TechInfo>()).Select(Reach).DefaultIfEmpty(0).Max();
        var reach = fleet.Ships.SelectMany(s => s.Weapons.Where(w => w.Mode != 3)).Select(w => Reach(w.Tech)).DefaultIfEmpty(0).Max();
        if (guns <= reach || fleet.Ships.Count == 0)
        {
            return 0;
        }

        var speed = fleet.Ships.Min(s => s.CombatSpeed());
        if (speed <= 0)
        {
            return settings.MostApproachRounds;
        }

        var steps = (guns - reach) / speed;
        return (int)Math.Clamp(Math.Ceiling(steps / settings.ApproachStepsPerRound), 0, settings.MostApproachRounds);
    }
}
