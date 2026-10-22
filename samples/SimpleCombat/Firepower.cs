using OpenPax.Sim;
using OpenPax.Sim.Players;

namespace SimpleCombat;

/// <summary>
/// A side's weapons and defenses, boiled down from the battle rules the
/// original fights step by step (docs/sim/combat.md, combat-step.md), so a
/// round can be worked out at once. Weapon damage is each weapon's best
/// range band by its condition, as the original's quick resolve takes it.
/// </summary>
public sealed class Firepower
{
    private double _beamAim;
    private double _missileAim;
    private double _droneAim;
    private double _hull;
    private double _ecm;
    private int _defended;

    /// <summary>Energy weapons, projectiles and area weapons in the three firing arcs.</summary>
    public double Beam { get; private set; }

    /// <summary>Weapons whose shots home on their target.</summary>
    public double Missile { get; private set; }

    /// <summary>Fighter flights: three craft each, each with its weapons.</summary>
    public double Drones { get; private set; }

    /// <summary>Point defense mounts: in battle they fire all round at missiles, fighters and mines within 350.</summary>
    public double PointDefense { get; private set; }

    /// <summary>Weapons of reach 500 or less (their two longer bands empty): in close, they can also take on small craft.</summary>
    public double ShortRange { get; private set; }

    /// <summary>Planet bombs, which only strike planets, and area weapons, which strike planets too.</summary>
    public double Bombs { get; private set; }

    /// <summary>What a beam aims with: sensor accuracy and experience (CombatAttack) plus the weapon's own.</summary>
    public double BeamAim => Beam > 0 ? _beamAim / Beam : 0;

    /// <summary>A missile's tracking (the weapon's flag byte, signed), what ECM must beat.</summary>
    public double MissileAim => Missile > 0 ? _missileAim / Missile : 0;

    public double DroneAim => Drones > 0 ? _droneAim / Drones : 0;

    /// <summary>Hull defense (hull, experience, drive), what a beam must beat; an orbital has none.</summary>
    public double HullDefense => _defended > 0 ? _hull / _defended : 0;

    /// <summary>ECM strength, what a missile must beat.</summary>
    public double Ecm => _defended > 0 ? _ecm / _defended : 0;

    /// <summary>Fighters screening the side: each flight takes its missile defense off incoming missiles' tracking.</summary>
    public double MissileScreen { get; private set; }

    /// <summary>Fighters weaving: their defense taken off the chance to hit them.</summary>
    public double DroneDodge { get; private set; }

    public int HighestSweeper { get; private set; }

    /// <summary>What the side has against missiles and drones.</summary>
    public double SmallCraftDefense(Settings settings) => PointDefense + (ShortRange * settings.ShortRangeVsSmallCraft) + (Drones * settings.DronesVsSmallCraft);

    public static Firepower Of(Game game, IEnumerable<Fleet> fleets)
    {
        var f = new Firepower();
        var flights = 0.0;
        foreach (var ship in fleets.SelectMany(x => x.Ships))
        {
            var aim = ship.CombatAttack();
            foreach (var w in ship.Weapons)
            {
                f.AddWeapon(w.Tech, w.Count * w.Condition / 100.0, w.Mode == 3, aim);
            }

            foreach (var c in ship.Fighters.Where(c => c.Tech.Kind == TechInfo.FighterKind))
            {
                var n = c.Operational;
                flights += n;
                f.AddFlights(game, c.Tech, n);
            }

            f._hull += ship.HullDefense();
            f._ecm += ship.EcmRating();
            f._defended++;
            f.HighestSweeper = Math.Max(f.HighestSweeper, game.MinesweeperLevel(ship));
        }

        f.FinishScreens(flights);
        return f;
    }

    /// <summary>A colony's defense structures, but for its mine fields: orbitals fire with an attack of 100 when a battle is resolved at once.</summary>
    public static Firepower Of(Game game, IEnumerable<Structure> orbitals)
    {
        var f = new Firepower();
        var flights = 0.0;
        foreach (var s in orbitals)
        {
            var share = s.MaxHitPoints > 0 ? s.Count * Math.Clamp(s.HitPoints / s.MaxHitPoints, 0, 1) : 0;
            var aim = 100 + (s.SensorTech?.SensorAccuracy ?? 0);
            foreach (var w in s.WeaponTechs.OfType<TechInfo>())
            {
                f.AddWeapon(w, share, false, aim);
            }

            if (s.SecondEnergyTech is { } pd)
            {
                f.AddWeapon(pd, share, true, aim);
            }

            foreach (var fighter in s.FighterTechs.OfType<TechInfo>())
            {
                flights += share;
                f.AddFlights(game, fighter, share);
            }
        }

        f.FinishScreens(flights);
        return f;
    }

    private void AddWeapon(TechInfo tech, double count, bool pointDefense, double aim)
    {
        var best = tech.Damage.Take(3).DefaultIfEmpty(0).Max() * count;
        if (tech.WeaponType == 1)
        {
            Bombs += best;
            return;
        }

        if (pointDefense)
        {
            PointDefense += tech.Damage[0] * count;
            return;
        }

        if (tech.WeaponType == 2)
        {
            Bombs += best;
        }

        if (tech.Tracks)
        {
            Missile += best;
            _missileAim += (sbyte)tech.WeaponFlag * best;
        }
        else
        {
            Beam += best;
            _beamAim += (aim + tech.WeaponAccuracy) * best;
        }

        if (tech.Damage.Count > 3 && tech.Damage[2] == 0 && tech.Damage[3] == 0)
        {
            ShortRange += tech.Damage[0] * count;
        }
    }

    private void AddFlights(Game game, TechInfo fighter, double flights)
    {
        var weapons = (int[])[fighter.FighterWeapon1, fighter.FighterWeapon2];
        var perCraft = weapons.Where(id => id != 0).Sum(id => game.Tables.Tech(id).Damage.Max());
        var damage = flights * 3 * perCraft;
        Drones += damage;
        _droneAim += fighter.FighterAccuracy * damage;
        MissileScreen += fighter.FighterMissileDefense * flights;
        DroneDodge += fighter.FighterDefense * flights;
    }

    private void FinishScreens(double flights)
    {
        // Averages over the side's flights; a screen thins out as the side grows past them.
        var guarded = Math.Max(1, Math.Max(flights, _defended));
        MissileScreen /= guarded;
        DroneDodge = flights > 0 ? DroneDodge / flights : 0;
    }
}
