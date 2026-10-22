using OpenPax.Sim.Galaxy;
using OpenPax.Sim.Players;

namespace OpenPax.Sim;

/// <summary>
/// The yearly money figures the Domestic Policy window shows. A year is ten
/// economy periods, so these are ten times what a period charges. Each is
/// computed as the original computes it, product order included, so the
/// lines need not add up to the net to the last unit.
/// </summary>
public sealed partial class Game
{
    /// <summary>Gross income less structure upkeep (Annual Income).</summary>
    public static double AnnualIncome(Player p)
    {
        return ((p.Income * 10.0 - p.StructureUpkeep[2]) - p.StructureUpkeep[0]) - p.StructureUpkeep[1];
    }

    /// <summary>What research (0), espionage (1) or construction (2) improvements cost a year at the spending percent.</summary>
    public static double SpendingCost(Player p, int field)
    {
        return p.Spending[field] * 0.01 * p.ImprovementUpkeep[field + 1];
    }

    /// <summary>Construction subsidies a year: 20 credits a funded point.</summary>
    public static double SubsidyCost(Player p)
    {
        return p.Funding * 20.0;
    }

    /// <summary>Structure and ship upkeep a year.</summary>
    public static double FixedUpkeep(Player p)
    {
        return ((p.StructureUpkeep[2] + p.StructureUpkeep[1]) + p.StructureUpkeep[0]) + p.Upkeep;
    }

    /// <summary>
    /// What is left of a year's income after improvements, subsidies and
    /// upkeep. Note the products run spending times upkeep, then a hundredth,
    /// unlike the single lines.
    /// </summary>
    public static double NetIncome(Player p)
    {
        var earning = p.Income * 10.0 - p.Spending[0] * p.ImprovementUpkeep[1] * 0.01
            - p.Spending[1] * p.ImprovementUpkeep[2] * 0.01
            - p.Spending[2] * p.ImprovementUpkeep[3] * 0.01;
        return earning - SubsidyCost(p) - FixedUpkeep(p);
    }

    /// <summary>A colony's improvement upkeep at its owner's spending percents, plus its structures'.</summary>
    public static double ColonyUpkeep(Colony c)
    {
        var s = c.Owner.Spending;
        var espionage = s[1] * 0.01 * c.ImprovementUpkeep[2];
        var research = s[0] * 0.01 * c.ImprovementUpkeep[1];
        var construction = s[2] * 0.01 * c.ImprovementUpkeep[3];
        return ((espionage + research) + construction) + c.StructureUpkeep;
    }

    /// <summary>
    /// The next (step 1) or previous (step -1) plan from the colony's, among
    /// the ten slots, skipping those with no plan.
    /// </summary>
    public static int StepPlan(Colony colony, int step)
    {
        return StepPlan(colony.Owner, colony.Plan, step);
    }

    /// <summary>The next or previous slot holding a plan, wrapping; the stock plans always exist.</summary>
    public static int StepPlan(Player player, int slot, int step)
    {
        const int Slots = Player.PlanSlots;
        var i = slot;
        do
        {
            i = ((i + step) % Slots + Slots) % Slots;
        }
        while (player.Plans[i] is null);
        return i;
    }

    /// <summary>
    /// Colony command op 4: the colony follows another plan. Refused while
    /// the planet is in battle; the owner is not checked.
    /// </summary>
    public static void SetColonyPlan(Planet planet, int plan)
    {
        if (planet.Colony is { } colony && !planet.InCombat && plan >= 0 && plan < Player.PlanSlots && colony.Owner.Plans[plan] is not null)
        {
            colony.Plan = plan;
        }
    }
}
