using OpenPax.Sim.Numerics;
using OpenPax.Sim.Players;

namespace OpenPax.Sim;

/// <summary>Spending a colony's construction points (docs/sim/economy.md).</summary>
public sealed partial class Game
{
    private const double SetupBuild = 999999999.0;
    private const double HitDamage = 10.0;

    /// <summary>
    /// At setup (<paramref name="all"/>) everything is finished at once;
    /// otherwise the period's points go to the shipyard, structures and
    /// improvements in turn, and what is left is lost.
    /// </summary>
    internal void ConstructColony(Colony colony, bool all, ref StepEvents events)
    {
        if (all)
        {
            BuildStructures(colony, SetupBuild, ref events);
            BuildShips(colony, SetupBuild);
            BuildImprovements(colony, SetupBuild);
            return;
        }

        var points = colony.ConstructionPoints;
        if (points > 0.0)
        {
            points = BuildShips(colony, points);
            colony.ConstructionPoints = points;
        }

        if (points >= 0.0 && !colony.Destroyed)
        {
            points = BuildStructures(colony, points, ref events);
            colony.ConstructionPoints = points;
        }

        if (points > 0.0 && !colony.Destroyed)
        {
            colony.ConstructionPoints = BuildImprovements(colony, points);
        }
    }

    /// <summary>
    /// With full support, repairs and finishes structures in list order.
    /// Below it, every structure risks a hit and the points are kept.
    /// </summary>
    private double BuildStructures(Colony colony, double points, ref StepEvents events)
    {
        var support = colony.Owner.Support;
        if (support >= 100.0)
        {
            foreach (var s in colony.Structures.ToList())
            {
                if (points <= 0.0)
                {
                    break;
                }

                points = BuildStructure(s, points);
            }

            return points;
        }

        var risk = 100.0 - support;
        foreach (var s in colony.Structures.ToList())
        {
            if (colony.Destroyed)
            {
                break;
            }

            if (Random.Next(100) + 1 <= risk)
            {
                HitStructure(s, HitDamage, ref events);
            }
        }

        return points;
    }

    /// <summary>Repairs or finishes a structure; returns the points not needed.</summary>
    internal double BuildStructure(Structure s, double points)
    {
        s.HitPoints = Math.Clamp(s.HitPoints, 0.0, s.MaxHitPoints);
        var max = s.MaxHitPoints;
        if (s.HitPoints == max)
        {
            return points;
        }

        var missing = max - s.HitPoints;
        var cost = (double)(TechLevels.BuildCost(s.Tech, s.Colony.Owner) * s.Count);
        var buys = max / cost * points;
        if (buys < missing)
        {
            s.HitPoints = buys + s.HitPoints;
            return 0.0;
        }

        points = buys > missing ? Math.Max(0.0, points - cost / max * missing) : 0.0;
        s.HitPoints = max;
        s.UnderWork = false;
        return points;
    }

    /// <summary>
    /// A hit of 10. A missile base or battle station also draws for damage
    /// to one of three parts. A stack brought to nothing loses its units,
    /// and if that leaves the colony with less standing, its population
    /// shrinks in proportion.
    /// </summary>
    /// <returns>Whether the structure still stands.</returns>
    private bool HitStructure(Structure s, double amount, ref StepEvents events)
    {
        var colony = s.Colony;
        var before = Standing(colony);

        s.HitPoints = Math.Clamp(s.HitPoints, 0.0, s.MaxHitPoints);
        if (s.Tech.Kind is TechInfo.MissileBaseKind or TechInfo.BattleStationKind)
        {
            switch (Random.Next(100) % 10)
            {
                case 1:
                    s.SubsystemDamage[0] += amount;
                    break;
                case 5:
                    s.SubsystemDamage[1] += amount;
                    break;
                case 7:
                    s.SubsystemDamage[2] += amount;
                    break;
            }
        }

        s.HitPoints = Math.Max(0.0, s.HitPoints - amount);
        int left;
        if (s.HitPoints > 0.0)
        {
            left = s.HitPoints == s.MaxHitPoints ? s.Count : X87.ToInt(s.Count * (s.HitPoints / s.MaxHitPoints) - -1.0);
        }
        else
        {
            while (colony.Structures.Contains(s))
            {
                RemoveStructureUnit(s);
            }

            left = 0;
        }

        if (left > 0)
        {
            return true;
        }

        var after = Standing(colony);
        SetColonyResource(colony, Colony.Population, (double)after / before * colony.CurrentPopulation, ref events);
        return false;
    }

    /// <summary>Units in the first stack of shipyards, kind 6, missile bases, kind 9 and battle stations, plus improvements begun.</summary>
    private static int Standing(Colony colony)
    {
        var units = 0;
        foreach (var kind in (int[])[TechInfo.ShipyardKind, TechInfo.TrainingBaseKind, TechInfo.MissileBaseKind, 9, TechInfo.BattleStationKind])
        {
            units += colony.FirstOfKind(kind)?.Count ?? 0;
        }

        return units + colony.Improvements.Count(i => i is not null && i.Percent > 0.0);
    }


    /// <summary>Improvements in the colony's plan order until the points run out.</summary>
    private double BuildImprovements(Colony colony, double points)
    {
        var next = NextImprovement(colony);
        while (points > 0.0 && next is not null)
        {
            points = BuildImprovement(colony, next, points);
            if (points > 0.0)
            {
                next = NextImprovement(colony);
            }
        }

        return points;
    }

    public Improvement? NextImprovement(Colony colony)
    {
        foreach (var id in colony.Owner.Plans[colony.Plan] ?? [])
        {
            if (colony.Improvements[ImprovementSlot(Tables.Tech(id))] is { Percent: < 100.0 } improvement)
            {
                return improvement;
            }
        }

        return null;
    }

    /// <summary>Takes construction off an improvement; a finished one loses its effect. Returns the damage not used.</summary>
    private double DamageImprovement(Colony colony, Improvement improvement, double damage)
    {
        var built = Math.Clamp(improvement.Built, 0.0, improvement.Total);
        if (built < damage)
        {
            damage -= built;
            improvement.Built = 0.0;
        }
        else
        {
            improvement.Built = built - damage;
            damage = 0.0;
        }

        if (built == improvement.Total && improvement.Built != improvement.Total)
        {
            ApplyImprovement(colony, improvement, false);
        }

        // The original then tells the owner when one is wiped out (virtual +0x74), a notice only.
        return damage;
    }

    /// <summary>Builds an improvement; on completion its effect applies. Returns the points not needed.</summary>
    private double BuildImprovement(Colony colony, Improvement improvement, double points)
    {
        improvement.Built = Math.Clamp(improvement.Built, 0.0, improvement.Total);
        if (improvement.Built == improvement.Total)
        {
            return points;
        }

        var missing = improvement.Total - improvement.Built;
        var cost = (double)TechLevels.BuildCost(improvement.Tech, colony.Owner);
        var buys = improvement.Total / cost * points;
        if (buys < missing)
        {
            improvement.Built = buys + improvement.Built;
            return 0.0;
        }

        // Unlike structures, what is left is not clamped at 0.
        points = buys > missing ? points - cost / improvement.Total * missing : 0.0;
        improvement.Built = improvement.Total;
        ApplyImprovement(colony, improvement, true);
        return points;
    }
}
