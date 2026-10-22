using OpenPax.Sim;
using OpenPax.Sim.Galaxy;
using OpenPax.Sim.Players;

namespace OpenPax.Ai;

/// <summary>Where a computer player's warship fleets go (+0xFFC and +0xFF4).</summary>
internal static class WarFleets
{
    private readonly record struct Target(double Score, ISpaceObject Place);

    private static int Highest(Target a, Target b) => a.Score > b.Score ? -1 : b.Score > a.Score ? 1 : 0;

    private static int Lowest(Target a, Target b) => a.Score < b.Score ? -1 : b.Score < a.Score ? 1 : 0;

    public static void Run(Game game, Player p)
    {
        if (!Reset(game, p))
        {
            return;
        }

        Attack(game, p);
        Patrol(game, p);
    }

    /// <summary>Fleets with ships that are not colony fleets, in fleet list order.</summary>
    private static List<Fleet> Warfleets(Player p)
    {
        return p.Fleets.Where(f => !Fleets.IsColonyFleet(f) && f.Ships.Count > 0).ToList();
    }

    private static bool SomeIdle(Player p)
    {
        return Warfleets(p).Any(f => f.Orders.Count == 0 && !f.InCombat);
    }

    /// <summary>Every warship fleet not retreating stops where it is (each draws a new orbit).</summary>
    // it walks the whole fleet list, so empty fleets count too
    private static bool Reset(Game game, Player p)
    {
        var any = false;
        foreach (var fleet in p.Fleets.ToList())
        {
            if (!Fleets.IsColonyFleet(fleet) && !fleet.Retreating)
            {
                Fleets.Stop(game, fleet);
                any = true;
            }
        }

        return any;
    }

    private static void Attack(Game game, Player p)
    {
        Merge(game, p);
        if (!SomeIdle(p))
        {
            return;
        }

        var targets = TargetList(game, p);
        SendStrong(game, p, targets);

        var noOrders = Warfleets(p).All(f => f.Orders.Count == 0);
        var buildingWarship = p.Colonies.Any(c => c.FirstOfKind(TechInfo.ShipyardKind) is { Queue.Count: > 0 } yard && !Fleets.HasColonizer(yard.Queue[0]));
        if (noOrders && !buildingWarship)
        {
            SendAll(game, p, targets);
        }
    }

    /// <summary>Each idle fleet with room heads for the first other fleet with room it can reach safely.</summary>
    private static void Merge(Game game, Player p)
    {
        foreach (var a in Warfleets(p))
        {
            if (a.Orders.Count > 0 || a.Ships.Count >= Fleet.MaxShips)
            {
                continue;
            }

            foreach (var b in Warfleets(p))
            {
                if (b != a && b.Ships.Count < Fleet.MaxShips && Fleets.SendSafely(game, a, b, b.Star))
                {
                    break;
                }
            }
        }
    }

    /// <summary>Colonies, then fleets, of every player at war with this one, weakest first.</summary>
    private static Target[] TargetList(Game game, Player p)
    {
        var list = new List<Target>();
        foreach (var o in game.Players)
        {
            if (o == p || p.Treaties[o.Index].Level() >= 1 || o.Eliminated)
            {
                continue;
            }

            foreach (var c in o.Colonies)
            {
                list.Add(new Target(HostileStrength(game, p, c.Planet), c.Planet));
            }

            foreach (var f in o.Fleets)
            {
                list.Add(new Target(HostileStrength(game, p, f), f));
            }
        }

        var array = list.ToArray();
        MsvcSort.Sort<Target>(array, Lowest);
        return array;
    }

    /// <summary>The first idle fleet strong enough (caution times the target) that can reach a target takes it; no danger test.</summary>
    private static void SendStrong(Game game, Player p, Target[] targets)
    {
        foreach (var target in targets)
        {
            var needed = p.Caution * target.Score;
            foreach (var fleet in Warfleets(p))
            {
                if (fleet.Orders.Count == 0 && needed <= fleet.Strength && Send(game, fleet, target.Place))
                {
                    break;
                }
            }
        }
    }

    /// <summary>With nobody under way and no warship being built, every fleet goes for the weakest target it can reach.</summary>
    private static void SendAll(Game game, Player p, Target[] targets)
    {
        foreach (var fleet in Warfleets(p))
        {
            foreach (var target in targets)
            {
                if (Send(game, fleet, target.Place))
                {
                    break;
                }
            }
        }
    }

    /// <summary>Order to a place in the same star, or by a route in range; no danger test.</summary>
    private static bool Send(Game game, Fleet fleet, ISpaceObject place)
    {
        var star = Fleets.StarOf(game, place);
        if (fleet.Star == star || game.PlanRoute(fleet, star))
        {
            game.GiveOrder(fleet, Order.To(place));
            return true;
        }

        return false;
    }

    /// <summary>Green fleets to training bases, then idle fleets to shipyards, then to the weakest own colonies.</summary>
    private static void Patrol(Game game, Player p)
    {
        if (SomeIdle(p))
        {
            var academies = p.Colonies
                .Where(c => c.FirstOfKind(TechInfo.TrainingBaseKind) is { Percent: >= 100.0 })
                .Select(c => new Target(c.FirstOfKind(TechInfo.TrainingBaseKind)!.Count, c.Planet))
                .ToArray();
            MsvcSort.Sort<Target>(academies, Highest);

            // a ship below experience level 2.
            SendIdle(game, p, academies, f => f.Ships.Any(s => s.ExperienceLevel < 2));
        }

        if (SomeIdle(p))
        {
            var yards = p.Colonies
                .Where(c => c.FirstOfKind(TechInfo.ShipyardKind) is { Percent: >= 100.0 })
                .Select(c => new Target(Game.YardWork(c.FirstOfKind(TechInfo.ShipyardKind)!, c.Resources[Colony.Construction] * 10.0), c.Planet))
                .ToArray();
            MsvcSort.Sort<Target>(yards, Highest);
            SendIdle(game, p, yards, _ => true);
        }

        if (SomeIdle(p))
        {
            var own = p.Colonies.Select(c => new Target(game.ColonyStrength(c), c.Planet)).ToArray();
            MsvcSort.Sort<Target>(own, Lowest);
            SendIdle(game, p, own, _ => true);
        }
    }

    private static void SendIdle(Game game, Player p, Target[] places, Func<Fleet, bool> wanted)
    {
        foreach (var fleet in Warfleets(p))
        {
            if (fleet.Orders.Count > 0 || !wanted(fleet))
            {
                continue;
            }

            foreach (var place in places)
            {
                if (Fleets.SendSafely(game, fleet, place.Place, Fleets.StarOf(game, place.Place)))
                {
                    break;
                }
            }
        }
    }

    /// <summary>
    /// Strength of players at war with this one at a place: a colony and
    /// idle fleets circling it, or a fleet plus what shares its orbit
    /// (an idle fleet circling counts itself twice, as in the original).
    /// </summary>
    private static double HostileStrength(Game game, Player p, ISpaceObject place)
    {
        bool Hostile(Player o) => o != p && p.Treaties[o.Index].Level() < 1;

        double AtCenter(ISpaceObject center, double sum)
        {
            if (center is Planet { Colony: { } colony } && Hostile(colony.Owner))
            {
                sum = game.ColonyStrength(colony) + sum;
            }

            foreach (var f in game.Universe.Stars[Fleets.StarOf(game, center)].Fleets)
            {
                if (f.OrbitCenter == center && f.Orders.Count == 0 && Hostile(f.Owner))
                {
                    sum = f.Strength + sum;
                }
            }

            return sum;
        }

        if (place is Fleet fleet)
        {
            var sum = Hostile(fleet.Owner) ? (double)fleet.Strength : 0.0;
            return fleet.OrbitCenter is { } center ? AtCenter(center, sum) : sum;
        }

        var planetSum = 0.0;
        if (place is Planet { Colony: { } c } && Hostile(c.Owner))
        {
            planetSum = game.ColonyStrength(c);
        }

        foreach (var f in game.Universe.Stars[Fleets.StarOf(game, place)].Fleets)
        {
            if (f.OrbitCenter == place && f.Orders.Count == 0 && Hostile(f.Owner))
            {
                planetSum = f.Strength + planetSum;
            }
        }

        return planetSum;
    }
}
