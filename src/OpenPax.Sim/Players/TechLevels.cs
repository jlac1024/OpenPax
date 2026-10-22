namespace OpenPax.Sim.Players;

/// <summary>Tech levels and the sizes and costs they scale (docs/sim/ships.md).</summary>
public static class TechLevels
{
    // The original's table holds i cubed for i = 0 to 304.
    private const int TableTop = 304;

    /// <summary>The cube root rounded down; 0 for negative values.</summary>
    // (binary search over the cube table filled at)
    public static int Level(long value)
    {
        if (value < 0)
        {
            return 0;
        }

        int low = 0, high = TableTop;
        while (low < high)
        {
            var mid = (low + high + 1) / 2;
            if ((long)mid * mid * mid <= value)
            {
                low = mid;
            }
            else
            {
                high = mid - 1;
            }
        }

        return low;
    }

    /// <summary>A tech's size for a player; null for the unscaled size.</summary>
    public static int Size(TechInfo tech, Player? player)
    {
        return Scaled(tech, tech.Size, player);
    }

    /// <summary>A tech's cost for a player; null for the unscaled cost.</summary>
    public static int Cost(TechInfo tech, Player? player)
    {
        return Scaled(tech, tech.Cost, player);
    }

    /// <summary>Research cost: 125, 100 or 75 percent for a species research level of -1, 0 or 1 in the tech's field.</summary>
    public static int ResearchCost(TechInfo tech, Player? player)
    {
        if (player is null)
        {
            return tech.ResearchCost;
        }

        var percent = player.Species.ResearchLevels[tech.ResearchField] switch
        {
            -1 => 125,
            1 => 75,
            _ => 100,
        };
        return tech.ResearchCost * percent / 100;
    }

    /// <summary>Construction a tech costs to build: 80 percent for a builder species on improvements and specials.</summary>
    public static int BuildCost(TechInfo tech, Player? player)
    {
        var percent = player is not null && player.Species.Is(Species.Builder)
            && tech.ClassId is TechInfo.SpecialClass or TechInfo.ImprovementClass ? 80 : 100;
        return tech.BuildCost * percent / 100;
    }

    /// <summary>
    /// Full until the player's research level is 16 past the tech's, then
    /// half a percent less per level, down to half.
    /// </summary>
    private static int Scaled(TechInfo tech, int value, Player? player)
    {
        if (player is null || tech.ClassId == TechInfo.ImprovementClass)
        {
            return value;
        }

        var percent = (Level(tech.ResearchCost) - Level(player.ResearchTotal) + 215) * 5;
        percent = Math.Clamp(percent, 500, 1000);
        return value * percent / 1000;
    }
}
