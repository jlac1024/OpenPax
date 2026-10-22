using OpenPax.Sim.Players;

namespace OpenPax.Sim;

/// <summary>
/// The planetary profile editor's orders on the plan library (colony
/// command ops 5 to 8, run). Slots 0 to 5 are the stock plans;
/// the player's own go in 6 to 9.
/// </summary>
public sealed partial class Game
{
    /// <summary>Op 7: a copy of a plan in an empty slot, named as given ("Unnamed" in the game).</summary>
    public static void CopyPlan(Player player, int from, int to, string name)
    {
        if (player.Plans[from] is { } plan && to is >= 0 and < Player.PlanSlots && player.Plans[to] is null)
        {
            player.Plans[to] = [.. plan];
            player.PlanNames[to] = name;
        }
    }

    /// <summary>Op 6: the plan goes and its colonies follow plan 0.</summary>
    public static void DeletePlan(Player player, int slot)
    {
        if (slot is < 0 or >= Player.PlanSlots)
        {
            return;
        }

        player.Plans[slot] = null;
        player.PlanNames[slot] = null;
        foreach (var colony in player.Colonies.Where(c => c.Plan == slot))
        {
            colony.Plan = 0;
        }
    }

    /// <summary>Op 8: the plan's new name.</summary>
    public static void RenamePlan(Player player, int slot, string name)
    {
        if (slot is >= 0 and < Player.PlanSlots && player.Plans[slot] is not null)
        {
            player.PlanNames[slot] = name;
        }
    }

    /// <summary>
    /// Op 5: an entry moves to stand before another, or to the
    /// end when "to" is past the last.
    /// </summary>
    public static void MovePlanEntry(Player player, int slot, int from, int to)
    {
        if (slot is < 0 or >= Player.PlanSlots || player.Plans[slot] is not { } plan || from < 0 || from >= plan.Length)
        {
            return;
        }

        var moved = plan[from];
        var list = new List<int>(plan.Length);
        for (var i = 0; i < plan.Length; i++)
        {
            if (i == to)
            {
                list.Add(moved);
            }

            if (i != from)
            {
                list.Add(plan[i]);
            }
        }

        if (to >= plan.Length)
        {
            list.Add(moved);
        }

        player.Plans[slot] = [.. list];
    }
}
