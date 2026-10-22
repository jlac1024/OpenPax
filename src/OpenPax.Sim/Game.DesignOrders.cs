using OpenPax.Sim.Players;
using OpenPax.Sim.Setup;

namespace OpenPax.Sim;

/// <summary>
/// The Ship Design window's orders: starting a design, the builder's
/// AUTO, and the design command (type 6, run) that saves a
/// design into a slot (op 0) or deletes one (op 1).
/// </summary>
public sealed partial class Game
{
    /// <summary>
    /// A design to edit: a copy of an existing one,
    /// or a blank scout of armor class NONE; either way the player's best
    /// armor is fitted, replacing any other.
    /// </summary>
    public Design StartDesign(Player player, Design? from, int hull = 0)
    {
        var design = from?.Copy() ?? new Design(Tables.Hulls[hull]);
        design.Owner = player;
        design.Recalculate();
        if (player.BestArmor > 0)
        {
            design.Add(Tables.Tech(player.BestArmor), Component.NotWeapon);
        }

        return design;
    }

    /// <summary>AUTO: a blank design of the hull with the best armor, filled by the builder as computer players' designs are.</summary>
    public Design AutoDesign(Player player, int hull, string name)
    {
        var design = StartDesign(player, null, hull);
        DesignBuilder.Build(this, hull, design, player);
        design.Name = name;
        return design;
    }

    /// <summary>
    /// Op 0: a copy of the design goes in the first free slot
    /// with the next class number and a new design id. Quirk kept: with all
    /// 100 slots taken nothing happens. Returns the slot, or -1.
    /// </summary>
    public int SaveDesign(Player player, Design design)
    {
        var copy = design.Copy();
        copy.Slot = -1;
        PlayerCreation.AddDesign(this, player, copy);
        return copy.Slot;
    }

    /// <summary>Op 1: the slot is emptied. Ships already built keep their own copy.</summary>
    public static void DeleteDesign(Player player, int slot)
    {
        if (slot >= 0 && slot < player.Designs.Length)
        {
            player.Designs[slot] = null;
        }
    }
}
