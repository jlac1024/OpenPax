using OpenPax.Sim;
using OpenPax.Sim.Galaxy;
using OpenPax.Sim.Players;

namespace OpenPax.Ai;

/// <summary>Where a computer player orders shipyards, defenses and training bases (AI_BuildArtif).</summary>
internal static class Building
{
    private const int FighterBaseKind = 9;
    private const int MinefieldKind = 10;

    public static void Run(Game game, Player p, Budget budget)
    {
        while (budget.Afford(Budget.Shipyards, 0.0) && BuildShipyard(game, p, budget))
        {
        }

        BuildDefenses(game, p, budget);
        if (!p.Species.Is(Species.Warrior))
        {
            while (budget.Afford(Budget.Training, 0.0) && BuildTraining(game, p, budget))
            {
            }
        }
    }

    /// <summary>
    /// Grows a yard below the tech's level, else founds one at a star with
    /// none, at the colony with the most construction (at least 20).
    /// </summary>
    private static bool BuildShipyard(Game game, Player p, Budget budget)
    {
        var best = game.BestOfKind(p, TechInfo.ShipyardKind);
        if (best < 0)
        {
            return false;
        }

        var tech = game.Tables.Tech(best);
        if (!budget.Spare(TechLevels.Cost(tech, p)))
        {
            return false;
        }

        var site = p.Colonies.FirstOrDefault(c => c.FirstOfKind(TechInfo.ShipyardKind) is { } yard
            && yard.Count < tech.InstallationLevel && Game.Damaged(c) is null)?.Planet;
        if (site is null)
        {
            var most = 200.0;
            foreach (var c in p.Colonies)
            {
                var star = game.Universe.Stars[c.Star];
                if (star.Planets.Any(pl => pl.Colony is { } other && other.Owner == p && other.FirstOfKind(TechInfo.ShipyardKind) is not null))
                {
                    continue;
                }

                foreach (var planet in star.Planets)
                {
                    if (planet.Colony is { } colony && colony.Owner == p)
                    {
                        var v = colony.Resources[Colony.Construction] * 10.0;
                        if (v >= most)
                        {
                            most = v;
                            site = planet;
                        }
                    }
                }
            }
        }

        if (site is null)
        {
            return false;
        }

        game.OrderStructure(site, best);
        return true;
    }

    /// <summary>
    /// At each finished, productive colony with few defenses for its
    /// population, one more defense of a kind drawn at random among those
    /// allowed and affordable.
    /// </summary>
    private static void BuildDefenses(Game game, Player p, Budget budget)
    {
        var cap = game.Setup.Difficulty switch
        {
            1 => 2,
            2 => 3,
            3 => 4,
            _ => 5,
        };

        foreach (var c in p.Colonies.ToList())
        {
            if (Game.Damaged(c) is not null || !(c.Resources[Colony.Construction] * 10.0 >= 100.0))
            {
                continue;
            }

            var population = c.CurrentPopulation;
            var defenses = 0.0;
            foreach (var s in c.Structures)
            {
                if (s.Tech.Kind is FighterBaseKind or TechInfo.MissileBaseKind or TechInfo.BattleStationKind or MinefieldKind)
                {
                    defenses = s.Count + defenses;
                }
            }

            bool Wanted(int kind) => game.StructureAllowed(c, kind) && budget.CanAfford(kind)
                && !(c.FirstOfKind(kind) is { } stack && stack.Count >= cap);

            var wanted = new bool[MinefieldKind + 1];
            foreach (var kind in (int[])[FighterBaseKind, TechInfo.MissileBaseKind, TechInfo.BattleStationKind, MinefieldKind])
            {
                wanted[kind] = Wanted(kind);
            }

            if (!(defenses < population * 0.5) || !wanted.Any(w => w))
            {
                continue;
            }

            int kindDrawn;
            do
            {
                kindDrawn = game.Random.Next(4) + 7;
            }
            while (!wanted[kindDrawn]);

            game.OrderStructure(c.Planet, game.BestOfKind(p, kindDrawn));
        }
    }

    /// <summary>One training base site per empire, grown to the tech's level, at the first finished shipyard colony.</summary>
    private static bool BuildTraining(Game game, Player p, Budget budget)
    {
        var best = game.BestOfKind(p, TechInfo.TrainingBaseKind);
        if (best < 0)
        {
            return false;
        }

        var tech = game.Tables.Tech(best);
        if (!budget.Spare(TechLevels.Cost(tech, p)))
        {
            return false;
        }

        Planet? site = p.Colonies.FirstOrDefault(c => c.FirstOfKind(TechInfo.TrainingBaseKind) is { } stack
            && stack.Count < tech.InstallationLevel && Game.Damaged(c) is null)?.Planet;
        if (site is null)
        {
            if (p.Colonies.Any(c => c.FirstOfKind(TechInfo.TrainingBaseKind) is not null))
            {
                return false;
            }

            site = p.Colonies.FirstOrDefault(c => c.FirstOfKind(TechInfo.ShipyardKind) is not null
                && c.FirstOfKind(TechInfo.TrainingBaseKind) is null && Game.Damaged(c) is null)?.Planet;
        }

        if (site is null)
        {
            return false;
        }

        game.OrderStructure(site, best);
        return true;
    }
}
