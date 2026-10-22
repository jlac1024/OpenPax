using OpenPax.Sim.Players;
using OpenPax.Sim.Setup;

namespace OpenPax.Sim.Commands;

/// <summary>A design as data: hull type, quality, name and each component's tech, mode and count, in slot order.</summary>
public sealed record DesignSpec(int Hull, int Quality, string Name, DesignPart[] Parts)
{
    public static DesignSpec Of(Design design) =>
        new(design.Type, design.Quality, design.Name, [.. design.Components.Select(c => new DesignPart(c.Tech.Id, c.Mode, c.Count))]);

    /// <summary>The design again, for the player, fitted part by part as the Ship Design window fits them.</summary>
    internal Design Build(GameTables tables, Player owner)
    {
        var design = new Design(tables.Hulls[Hull]) { Name = Name, Owner = owner };
        design.SetQuality(Quality);
        foreach (var part in Parts)
        {
            for (var i = 0; i < part.Count; i++)
            {
                design.Add(tables.Tech(part.Tech), part.Mode);
            }
        }

        return design;
    }
}
