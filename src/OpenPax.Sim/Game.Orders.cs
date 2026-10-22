using OpenPax.Sim.Galaxy;
using OpenPax.Sim.Numerics;
using OpenPax.Sim.Players;

namespace OpenPax.Sim;

/// <summary>Orders players give their colonies, and the setters behind them (docs/sim/economy.md).</summary>
public sealed partial class Game
{
    /// <summary>Orders a structure at a planet's colony (mode 2).</summary>
    public void OrderStructure(Planet planet, int tech)
    {
        if (planet.Colony is { } colony && tech >= 0)
        {
            AddStructure(colony, Tables.Tech(tech));
        }
    }

    /// <summary>Orders a ship of a design slot at the colony's first shipyard, at the front of its queue (mode 1).</summary>
    public void OrderShip(Planet planet, int designSlot)
    {
        if (planet.Colony?.FirstOfKind(TechInfo.ShipyardKind) is { } yard && planet.Colony.Owner.Designs[designSlot] is { } design)
        {
            QueueShip(yard, NewShip(design));
        }
    }

    /// <summary>Sets a colony's construction funding, keeping the player's total in step.</summary>
    public static void SetFunding(Colony colony, double value)
    {
        var p = colony.Owner;
        p.Funding = Math.Max(0.0, p.Funding + colony.Funding * -1.0);
        colony.Funding = value;
        p.Funding = Math.Max(0.0, p.Funding + value);
        if (colony.Funding < 0.0)
        {
            colony.Funding = 0.0;
        }
    }

    /// <summary>Sets a spending percent (0 research, 1 espionage, 2 construction), whole and within 0 to 100.</summary>
    public static void SetSpending(Player p, int field, double value)
    {
        p.Spending[field] = Math.Clamp(X87.ToInt(value), 0, 100);
    }

    /// <summary>The colony's first structure not fully built or repaired, or null.</summary>
    public static Structure? Damaged(Colony colony)
    {
        return colony.Structures.FirstOrDefault(s => s.Percent < 100.0);
    }

    /// <summary>The next improvement the colony's plan will build, or -1.</summary>
    public int NextImprovementId(Colony colony)
    {
        return NextImprovement(colony)?.Tech.Id ?? -1;
    }

    /// <summary>Whether anything at all can be researched now.</summary>
    public bool AnythingToResearch(Player p)
    {
        return Tables.Techs.Any(t => CanResearch(p, t));
    }
}
