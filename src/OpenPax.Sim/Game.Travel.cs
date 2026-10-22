using OpenPax.Sim.Numerics;
using OpenPax.Sim.Players;

namespace OpenPax.Sim;

/// <summary>Routes between stars, and how strong a colony stands (docs/ai/computer-players.md).</summary>
public sealed partial class Game
{
    /// <summary>
    /// Gives a fleet orders to each wormhole on the way to a star, if every
    /// star on the way is supplied or within the jumps left of one that is.
    /// </summary>
    // (the computer player's call: orders, no messages)
    internal bool PlanRoute(Fleet fleet, int toStar)
    {
        var route = Universe.Route(fleet.Star, toStar);
        if (!RangeOk(fleet, route))
        {
            return false;
        }

        for (var k = 1; k < route.Count; k++)
        {
            var from = Universe.Stars[route[k - 1]];
            var hole = from.Wormholes.First(w => w.Partner?.Star == route[k]);
            GiveOrder(fleet, Order.To(hole));
        }

        return true;
    }

    private bool RangeOk(Fleet fleet, List<int> route)
    {
        var jumps = fleet.JumpsLeft;
        for (var k = 1; k < route.Count; k++)
        {
            var next = route[k];
            if (Supplies(Universe.Stars[next], fleet.Owner))
            {
                jumps = fleet.JumpRange;
                continue;
            }

            jumps--;
            var reach = jumps;
            if (!(reach > 0 && Universe.Stars.Any(s => Universe.Hops(next, s.Index) <= reach && Supplies(s, fleet.Owner))))
            {
                return false;
            }
        }

        return true;
    }

    /// <summary>The sum of a colony's structure strengths.</summary>
    public int ColonyStrength(Colony colony)
    {
        var sum = 0;
        foreach (var s in colony.Structures)
        {
            sum += StructureStrength(s);
        }

        return sum;
    }

    /// <summary>
    /// A defensive rating: hit points, shield, weapons and fighters, scaled
    /// by condition; 0 for shipyards and training bases.
    /// </summary>
    public int StructureStrength(Structure s)
    {
        var kind = s.Tech.Kind;
        if (kind is TechInfo.ShipyardKind or TechInfo.TrainingBaseKind)
        {
            return 0;
        }

        var percent = s.Percent;
        var max = s.MaxHitPoints;
        var total = X87.ToInt(max);
        if (kind == 10)
        {
            var v = X87.ToInt(percent * (s.Count * 75) * 0.01);
            var r = v * s.Tech.InstallationStrength * (s.Tech.InstallationLevel / 250);
            return X87.ToInt(Math.Sqrt(r));
        }

        if (s.ShieldTech is { } shield)
        {
            var f = 1.0 - s.SubsystemDamage[0] / shield.HitPoints * s.Count;
            total = total + X87.ToInt((double)(shield.ShieldCapacity * s.Count) * f)
                + X87.ToInt((double)shield.ShieldPercent * f * max * 0.01);
        }

        var weapons = 0;
        foreach (var w in s.WeaponTechs.OfType<TechInfo>())
        {
            weapons += w.Damage.Sum() * s.Count * 2;
        }

        var fighters = 0;
        foreach (var f in s.FighterTechs.OfType<TechInfo>())
        {
            foreach (var id in (int[])[f.FighterWeapon1, f.FighterWeapon2])
            {
                if (id != 0)
                {
                    fighters += Tables.Tech(id).Damage.Sum();
                }
            }
        }

        total = s.Count * fighters * 6 + weapons + total;
        return X87.ToInt(Math.Sqrt(percent * total * 0.01));
    }
}
