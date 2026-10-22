using OpenPax.Sim.Setup;

namespace OpenPax.Sim.Players;

/// <summary>
/// A player's six colony plans: the planetary improvements in the order a
/// colony builds them (docs/sim/economy.md).
/// </summary>
internal static class ColonyPlans
{
    public const int Count = 6;

    // Plan 0 breaks cost ties by resource in this order.
    private static readonly int[] _balancedRanks = [3, 1, 0, 4, 2];

    // Plans 1 to 5 put one resource first.
    private static readonly int[] _favored = [-1, 4, 3, 1, 2, 0];

    private readonly record struct Entry(int Cost, int Id, int Resource, bool HasFlat);

    public static int[]?[] Build(GameTables tables, Player player)
    {
        var plans = new int[]?[Player.PlanSlots];
        for (var plan = 0; plan < Count; plan++)
        {
            var entries = tables.OfKind(TechInfo.ImprovementKind)
                .Select(t => new Entry(TechLevels.BuildCost(t, player), t.Id, t.Resource, t.Flat.Any(f => f > 0.0)))
                .ToArray();
            var favored = _favored[plan];
            MsvcSort.Sort<Entry>(entries, plan == 0 ? CompareBalanced : (a, b) => CompareFavoring(a, b, favored));
            plans[plan] = [.. entries.Select(e => e.Id)];
        }

        return plans;
    }

    private static int CompareBalanced(Entry a, Entry b)
    {
        if (a.Cost != b.Cost)
        {
            return a.Cost < b.Cost ? -1 : 1;
        }

        if (a.Resource != b.Resource)
        {
            foreach (var r in _balancedRanks)
            {
                if (a.Resource == r)
                {
                    return -1;
                }

                if (b.Resource == r)
                {
                    return 1;
                }
            }

            return 0;
        }

        // Quirk kept: two flat-valued improvements each sort before the other.
        if (a.HasFlat)
        {
            return -1;
        }

        return b.HasFlat ? 1 : 0;
    }

    private static int CompareFavoring(Entry a, Entry b, int resource)
    {
        if (a.Resource == resource && b.Resource != resource)
        {
            return -1;
        }

        if (b.Resource == resource && a.Resource != resource)
        {
            return 1;
        }

        return a.Cost < b.Cost ? -1 : a.Cost > b.Cost ? 1 : 0;
    }
}
