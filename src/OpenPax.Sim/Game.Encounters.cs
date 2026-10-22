using OpenPax.Sim.Galaxy;
using OpenPax.Sim.Players;

namespace OpenPax.Sim;

/// <summary>Fleets set to intercept (docs/sim/movement.md).</summary>
public sealed partial class Game
{
    /// <summary>
    /// A fleet set to intercept looks at the other players' fleets in its
    /// system. A hostile one, at war with its owner, or neutral
    /// and heading for one of its owner's fleets or colonies (or through a
    /// wormhole to a system with one), it goes after at once, dropping its
    /// orders and drawing a new orbit first. Quirk kept: meeting any other
    /// foreign fleet that is not hostile makes it give up chasing another
    /// player's fleet, drawing a new orbit too.
    /// </summary>
    internal void Encounter(Star star, Fleet fleet)
    {
        foreach (var other in star.Fleets.ToList())
        {
            if (other == fleet || !fleet.Intercepting || fleet.InCombat || other.Owner == fleet.Owner)
            {
                continue;
            }

            if (Hostile(fleet.Owner, other))
            {
                ClearOrders(fleet);
                fleet.EnterOrbit(Random, null);
                GiveOrder(fleet, Order.To(other));
                return;
            }

            if (fleet.Orders.FirstOrDefault()?.Target is Fleet chased && chased.Owner != fleet.Owner)
            {
                ClearOrders(fleet);
                fleet.EnterOrbit(Random, null);
            }
        }
    }

    /// <summary>Whether a fleet is a threat to a player: at war, or neutral and making for something of the player's.</summary>
    private bool Hostile(Player player, Fleet other)
    {
        var level = player.Treaties[other.Owner.Index].Level();
        if (level < Treaty.Neutral)
        {
            return true;
        }

        if (level != Treaty.Neutral || other.Orders.FirstOrDefault()?.Target is not { } target)
        {
            return false;
        }

        return target switch
        {
            Planet planet => planet.Colony?.Owner == player,
            Fleet aimed => aimed.Owner == player,
            Wormhole { Partner: { } far } => Universe.Stars[far.Star].Planets.Any(p => p.Colony?.Owner == player),
            _ => false,
        };
    }
}
