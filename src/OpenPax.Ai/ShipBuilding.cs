using OpenPax.Sim;
using OpenPax.Sim.Players;

namespace OpenPax.Ai;

/// <summary>Which ships a computer player orders, and where (AI_BuildShips, +0xFE0).</summary>
internal static class ShipBuilding
{
    private const int ColonyShipSlot = 2;

    public static void Run(Game game, Player p, Budget budget, int colonyShipCap)
    {
        if (Fleets.ColonyShipCount(p) < 1)
        {
            BuildColonyShip(game, p, budget, colonyShipCap);
        }

        if (Fleets.AtWar(game, p))
        {
            while (BuildWarship(game, p, budget))
            {
            }
        }

        while (BuildColonyShip(game, p, budget, colonyShipCap))
        {
        }
    }

    /// <summary>
    /// Always one colony ship when there is none; more only while others are
    /// on their way, up to the cap (the difficulty when the player was made).
    /// </summary>
    private static bool BuildColonyShip(Game game, Player p, Budget budget, int cap)
    {
        var count = Fleets.ColonyShipCount(p);
        if (count >= cap)
        {
            return false;
        }

        var traveling = Fleets.ColonyFleets(p).Any(f => f.Orders.Count > 0);
        if (!traveling)
        {
            if (count > 0)
            {
                return false;
            }
        }
        else
        {
            var upkeep = Fleets.Upkeep(p, p.Designs[ColonyShipSlot]);
            if (!budget.Afford(Budget.Ships, upkeep) || !budget.Spare(upkeep))
            {
                return false;
            }
        }

        if (BestYard(p) is not { } colony)
        {
            return false;
        }

        game.OrderShip(colony.Planet, ColonyShipSlot);
        return true;
    }

    /// <summary>
    /// Refits first; else the best usable warship type for the difficulty,
    /// with a coin toss between types 4 and 5, if it can be paid for.
    /// </summary>
    private static bool BuildWarship(Game game, Player p, Budget budget)
    {
        var difficulty = game.Setup.Difficulty;
        if ((difficulty > 2 && (Refit(game, p, budget, 4) || Refit(game, p, budget, 5)))
            || (difficulty > 1 && Refit(game, p, budget, 3))
            || Refit(game, p, budget, 1))
        {
            return true;
        }

        var type = 1;
        if (difficulty > 1 && Usable(game, p, p.Designs[3]))
        {
            type = 3;
        }

        if (difficulty > 2)
        {
            var four = Usable(game, p, p.Designs[4]);
            var five = Usable(game, p, p.Designs[5]);
            if (four)
            {
                type = 4;
            }

            if (five)
            {
                type = 5;
            }

            // The draw comes before any money or yard test.
            if (four && five)
            {
                type = game.Random.Next(100) + 1 <= 50 ? 4 : 5;
            }
        }

        var rich = Rich(game, p);
        var upkeep = Fleets.Upkeep(p, p.Designs[type]);
        if (!budget.Afford(Budget.Ships, upkeep) && !rich)
        {
            return false;
        }

        var spare = budget.Free() - budget.Reserve - budget.ShipQueue() - upkeep - Fleets.Upkeep(p, p.Designs[ColonyShipSlot]);
        if (spare < 0.0 && !rich)
        {
            return false;
        }

        if (BestYard(p) is not { } colony)
        {
            return false;
        }

        game.OrderShip(colony.Planet, type);
        return true;
    }

    /// <summary>
    /// A ship of an older design of the type, in an idle fleet circling a
    /// free yard's planet, is rebuilt to the current design; its condition
    /// counts toward the work.
    /// </summary>
    private static bool Refit(Game game, Player p, Budget budget, int type)
    {
        if (p.Designs[type] is not { } current)
        {
            return false;
        }

        var rich = Rich(game, p);
        foreach (var c in p.Colonies)
        {
            if (c.FirstOfKind(TechInfo.ShipyardKind) is not { Queue.Count: 0 } yard)
            {
                continue;
            }

            foreach (var fleet in game.Universe.Stars[c.Star].Fleets.ToList())
            {
                if (fleet.Owner != p || Fleets.IsColonyFleet(fleet) || fleet.OrbitCenter != c.Planet || fleet.InCombat)
                {
                    continue;
                }

                foreach (var ship in fleet.Ships.ToList())
                {
                    if (ship.Type != type || ship.Id >= current.Id)
                    {
                        continue;
                    }

                    var newUpkeep = Fleets.Upkeep(p, current);
                    var oldUpkeep = Fleets.Upkeep(p, ship);
                    if (!budget.Afford(Budget.Ships, newUpkeep - oldUpkeep) && !rich)
                    {
                        continue;
                    }

                    var spare = budget.Free() - budget.Reserve - budget.ShipQueue() - newUpkeep + oldUpkeep - Fleets.Upkeep(p, p.Designs[ColonyShipSlot]);
                    if (spare < 0.0 && !rich)
                    {
                        continue;
                    }

                    fleet.RemoveShip(ship, game.Tables.Tech);
                    var refit = current.Copy();
                    refit.TakeOver(ship);
                    Game.QueueShip(yard, refit);
                    var worth = (double)ship.BuildCost() * ((double)ship.ConditionPercent() * 0.01);
                    var cost = (double)refit.BuildCost();
                    yard.Progress = yard.Total * (cost <= worth ? 1.0 : worth / cost);
                    return true;
                }
            }
        }

        return false;
    }

    /// <summary>The design fits the best armor's hull limit and can make wormhole jumps.</summary>
    private static bool Usable(Game game, Player p, Design? design)
    {
        if (design is null || p.BestArmor <= 0)
        {
            return false;
        }

        return design.Type <= game.Tables.Tech(p.BestArmor).ArmorHullLimit && design.DriveRange() > 0;
    }

    /// <summary>At difficulty 4 and up, money of more than five times ship upkeep skips the money tests.</summary>
    private static bool Rich(Game game, Player p)
    {
        return game.Setup.Difficulty >= 4 && p.Money > p.Upkeep * 5.0;
    }

    /// <summary>The colony whose idle first shipyard works fastest; the later one on ties.</summary>
    private static Colony? BestYard(Player p)
    {
        var best = -99999.9;
        Colony? pick = null;
        foreach (var c in p.Colonies)
        {
            if (c.FirstOfKind(TechInfo.ShipyardKind) is not { Queue.Count: 0 } yard)
            {
                continue;
            }

            var work = Game.YardWork(yard, c.Resources[Colony.Construction] * 10.0);
            if (best <= work)
            {
                best = work;
                pick = c;
            }
        }

        return pick;
    }
}
