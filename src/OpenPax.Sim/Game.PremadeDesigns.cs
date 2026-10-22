using OpenPax.Sim.Players;
using OpenPax.Sim.Setup;

namespace OpenPax.Sim;

/// <summary>Premade ship designs from mods (docs/modding/content.md, GameTables.Designs).</summary>
public sealed partial class Game
{
    /// <summary>
    /// A human player gets each premade design once every part of it is
    /// known: at the start, and as techs are learned. A design is not given
    /// again while one of that name is in the player's slots; one the player
    /// deleted comes back with the next tech learned. Computer players build
    /// their own designs, and rebuild them as they learn, so they get none.
    /// </summary>
    internal void GivePremadeDesigns(Player player)
    {
        if (player.IsAi || Tables.Designs.Count == 0)
        {
            return;
        }

        foreach (var template in Tables.Designs)
        {
            if (template.Hull < 0 || template.Hull >= Tables.Hulls.Count
                || !template.Parts.All(p => player.Knows(p.Tech))
                || player.Designs.Any(d => d?.Name == template.Name))
            {
                continue;
            }

            PlayerCreation.AddDesign(this, player, Build(template));
        }
    }

    private Design Build(DesignTemplate template)
    {
        var design = new Design(Tables.Hulls[template.Hull]) { Name = template.Name };
        design.SetQuality(template.Quality);
        foreach (var (tech, mode, count) in template.Parts)
        {
            for (var i = 0; i < count; i++)
            {
                design.Add(Tables.Tech(tech), mode);
            }
        }

        return design;
    }
}
