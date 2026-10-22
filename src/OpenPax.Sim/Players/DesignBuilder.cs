using OpenPax.Sim.Numerics;
using OpenPax.Sim.Setup;

namespace OpenPax.Sim.Players;

/// <summary>
/// Picks components for a design of a hull type from what a player knows
/// (docs/sim/ships.md). Used for every design the game makes for players.
/// </summary>
internal static class DesignBuilder
{
    // Fractions of the hull's capacity (later of the space left) by hull type.
    private static readonly double[] _tactical = [.3, .3, .1, .3, .3, .25];
    private static readonly double[] _sensor = [.05, .05, 0, .05, .05, .02];
    private static readonly double[] _ecm = [.05, .05, 0, .05, .05, .05];
    private static readonly double[] _shield = [.1, .1, 0, .1, .1, .05];
    private static readonly double[] _fighters = [0, 0, 0, 0, 0, .43];
    private static readonly double[] _bombs = [0, .06, 0, .06, .06, .02];
    private static readonly double[] _firstEnergy = [.2, .2, 0, .2, .2, 1.0];
    private static readonly double[] _weaponsMode1 = [.15, .15, 0, .15, .15, 0];
    private static readonly double[] _weaponsMode2 = [.15, .15, 0, .15, .15, 0];

    private static readonly int[] _weaponKinds =
        [TechInfo.EnergyWeaponKind, TechInfo.ProjectileKind, TechInfo.MissileKind, TechInfo.AreaWeaponKind];

    private const int ColonyShipType = 2;
    private const int ColonyShipQuality = 0;
    private const int WarshipQuality = 4;
    private const int NoSizeLimit = 99999;

    public static void Build(Game game, int type, Design design, Player player)
    {
        var tables = game.Tables;
        design.SetHull(tables.Hulls[type]);
        if (player.IsAi)
        {
            design.Name = tables.DesignNames[type + 27];
        }

        var cap = design.Hull.Capacity;
        var rem = cap;

        // The star drive ignores space: the original works out a budget and never uses it.
        if (LastKnown(tables, player, TechInfo.StarDriveKind, t => DriveFits(t, type)) is { } drive)
        {
            design.Add(drive, Component.NotWeapon);
            rem = cap - TechLevels.Size(drive, player);
        }

        rem -= AddLastFitting(design, tables, player, TechInfo.TacticalDriveKind, Budget(cap, _tactical[type]));
        rem -= AddLastFitting(design, tables, player, TechInfo.SensorKind, Budget(cap, _sensor[type]));
        rem -= AddLastFitting(design, tables, player, TechInfo.EcmKind, Budget(cap, _ecm[type]));

        var shieldBudget = Budget(cap, _shield[type]);
        foreach (var kind in (int[])[TechInfo.FieldKind, TechInfo.ShieldKind, TechInfo.ScreenKind])
        {
            if (LastFitting(tables, player, kind, shieldBudget) is { } shield)
            {
                design.Add(shield, Component.NotWeapon);
                rem -= TechLevels.Size(shield, player);
                break;
            }
        }

        design.SetQuality(type == ColonyShipType ? ColonyShipQuality : WarshipQuality);

        if (player.IsAi && type is not (0 or ColonyShipType))
        {
            // Types 4 and 5 always want one and make no draw (C short circuit).
            var wants = type is 4 or 5 || game.Random.Next(100) + 1 < 31;
            if (wants)
            {
                rem -= AddLastFitting(design, tables, player, TechInfo.MinesweeperKind, NoSizeLimit);
            }
        }

        var fighterBudget = Budget(cap, _fighters[type]);
        while (LastFitting(tables, player, TechInfo.FighterKind, fighterBudget) is { } fighter)
        {
            design.Add(fighter, Component.NotWeapon);
            var size = TechLevels.Size(fighter, player);
            rem -= size;
            fighterBudget -= size;
        }

        if (type == ColonyShipType)
        {
            // a 0.7 budget is worked out and not used.
            if (LastKnown(tables, player, TechInfo.ColonizerKind, _ => true) is { } colonizer)
            {
                design.Add(colonizer, Component.NotWeapon);
                rem -= TechLevels.Size(colonizer, player);
            }
        }

        var bombBudget = Budget(cap, _bombs[type]);
        while (BestScoring(tables, player, [TechInfo.PlanetBombKind], bombBudget) is { } bomb)
        {
            design.Add(bomb, 0);
            var size = TechLevels.Size(bomb, player);
            rem -= size;
            bombBudget -= size;
        }

        var space = rem;

        // only the first energy weapon of all.
        var firstBudget = Budget(space, _firstEnergy[type]);
        var first = tables.OfKind(TechInfo.EnergyWeaponKind).FirstOrDefault();
        while (first is not null && player.KnownTechs.Contains(first.Id) && TechLevels.Size(first, player) <= firstBudget)
        {
            design.Add(first, 3);
            var size = TechLevels.Size(first, player);
            rem -= size;
            firstBudget -= size;
        }

        rem = FillWeapons(design, tables, player, Budget(space, _weaponsMode1[type]), 1, rem);
        rem = FillWeapons(design, tables, player, Budget(space, _weaponsMode2[type]), 2, rem);

        if (type != ColonyShipType)
        {
            while (BestScoring(tables, player, _weaponKinds, rem) is { } weapon)
            {
                design.Add(weapon, 0);
                rem -= TechLevels.Size(weapon, player);
            }
        }
    }

    private static int FillWeapons(Design design, GameTables tables, Player player, int budget, int mode, int rem)
    {
        while (BestScoring(tables, player, _weaponKinds, budget) is { } weapon)
        {
            design.Add(weapon, mode);
            var size = TechLevels.Size(weapon, player);
            rem -= size;
            budget -= size;
        }

        return rem;
    }

    /// <summary>trunc(base * fraction), written in the original as a sign test then a truncation.</summary>
    private static int Budget(int space, double fraction)
    {
        return X87.ToInt(space * fraction);
    }

    private static bool DriveFits(TechInfo drive, int type)
    {
        return drive.MaxHullClass switch
        {
            2 => type <= 2,
            3 => type == 3,
            5 => type is 4 or 5,
            _ => false,
        };
    }

    private static TechInfo? LastKnown(GameTables tables, Player player, int kind, Func<TechInfo, bool> test)
    {
        return tables.OfKind(kind).LastOrDefault(t => player.KnownTechs.Contains(t.Id) && test(t));
    }

    private static TechInfo? LastFitting(GameTables tables, Player player, int kind, int budget)
    {
        return LastKnown(tables, player, kind, t => TechLevels.Size(t, player) <= budget);
    }

    /// <summary>Adds the last known tech of a kind that fits, once; returns its size or 0.</summary>
    private static int AddLastFitting(Design design, GameTables tables, Player player, int kind, int budget)
    {
        if (LastFitting(tables, player, kind, budget) is not { } tech)
        {
            return 0;
        }

        design.Add(tech, Component.NotWeapon);
        return TechLevels.Size(tech, player);
    }

    /// <summary>
    /// The known weapon of the kinds with the best damage per reload per
    /// space; ties go to the later one. Missiles count 1.3, others 0.5.
    /// </summary>
    private static TechInfo? BestScoring(GameTables tables, Player player, int[] kinds, int budget)
    {
        TechInfo? best = null;
        var bestScore = 0.0;
        foreach (var kind in kinds)
        {
            foreach (var t in tables.OfKind(kind))
            {
                var size = TechLevels.Size(t, player);
                if (!player.KnownTechs.Contains(t.Id) || size > budget)
                {
                    continue;
                }

                var damage = (double)t.Damage[3] + t.Damage[2] + t.Damage[1] + t.Damage[0];
                var score = damage / t.Reload / size * (kind == TechInfo.MissileKind ? 1.3 : 0.5);
                if (bestScore <= score)
                {
                    best = t;
                    bestScore = score;
                }
            }
        }

        return best;
    }
}
