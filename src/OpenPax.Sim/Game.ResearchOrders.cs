using OpenPax.Sim.Players;

namespace OpenPax.Sim;

/// <summary>
/// A player's research orders from the Research window: the research
/// command (type 1, run) and what the window asks first.
/// </summary>
public sealed partial class Game
{
    /// <summary>Puts a tech in a field's slot, resuming any progress saved for it (op research).</summary>
    public void ResearchTech(Player player, int tech, int field)
    {
        AssignResearch(player, tech, field);
    }

    /// <summary>Empties a field's slot, keeping its progress for later (op stop).</summary>
    public static void StopResearch(Player player, int field)
    {
        var slot = player.ResearchSlots[field];
        if (slot.Tech != ResearchSlot.None)
        {
            player.SavedProgress[slot.Tech] = slot.Progress;
        }

        slot.Clear();
    }

    /// <summary>Sets a field's share of research, never past what the other four leave of 100 (op allocate).</summary>
    public static void SetAllocation(Player player, int field, int value)
    {
        var others = player.Research.Where((_, f) => f != field).Sum();
        player.Research[field] = Math.Min(value, Math.Max(0, 100 - others));
    }

    /// <summary>Whether the player could research the tech now: unknown, its prerequisites known, no academy for warriors.</summary>
    public static bool Researchable(Player player, TechInfo tech)
    {
        return CanResearch(player, tech);
    }

    /// <summary>A field of the player is researching the tech.</summary>
    public static bool BeingResearched(Player player, int tech)
    {
        return player.ResearchSlots.Any(s => s.Tech == tech);
    }

    /// <summary>The tech's research cost for this player, scaled by the species' level for its field.</summary>
    public static int ResearchCostFor(Player player, TechInfo tech)
    {
        return TechLevels.ResearchCost(tech, player);
    }
}
