using OpenPax.Sim.Galaxy;
using OpenPax.Sim.Players;

namespace OpenPax.Sim;

/// <summary>What the Planetary Surface window asks of a colony, and its subsidy order (docs/ui/planetary-surface.md).</summary>
public sealed partial class Game
{
    /// <summary>
    /// The colony's improvements not yet finished, in its plan's order; the
    /// first is what construction works on next.
    /// </summary>
    public IReadOnlyList<Improvement> UnfinishedImprovements(Colony colony)
    {
        var list = new List<Improvement>();
        foreach (var id in colony.Owner.Plans[colony.Plan] ?? [])
        {
            if (colony.Improvements[ImprovementSlot(Tables.Tech(id))] is { Percent: < 100.0 } improvement)
            {
                list.Add(improvement);
            }
        }

        return list;
    }

    /// <summary>
    /// Finished improvements by the resource each works on, 0 credits to 4
    /// population. Quirk kept: only exactly 100 percent counts.
    /// </summary>
    public static int[] FinishedByResource(Colony colony)
    {
        var counts = new int[5];
        foreach (var improvement in colony.Improvements)
        {
            if (improvement is { Percent: 100.0 })
            {
                counts[improvement.Tech.Resource]++;
            }
        }

        return counts;
    }

    /// <summary>
    /// Command 10, action 1050: a colony of the
    /// player gets the subsidy asked for, as a change from what it has. The
    /// player's total takes the same change, neither going below 0.
    /// </summary>
    public static void ChangeFunding(Player player, Planet planet, int value)
    {
        if (planet.Colony is not { } colony || colony.Owner != player)
        {
            return;
        }

        var d = (double)value - colony.Funding;
        colony.Funding += d;
        player.Funding = Math.Max(0.0, player.Funding + d);
        colony.Funding = Math.Max(0.0, colony.Funding);
    }
}
