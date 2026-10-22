using OpenPax.Sim.Galaxy;
using OpenPax.Sim.Players;

namespace OpenPax.Sim;

/// <summary>
/// A player's orders to one ship from the Fleet Control window: the ship
/// command (type 13, run). Both need the ship's fleet to be
/// the player's and not in battle.
/// </summary>
public sealed partial class Game
{
    private Design? Commanded(Player player, int shipId)
    {
        return FindShip(shipId) is { Fleet: { } fleet } ship && fleet.Owner == player && !fleet.InCombat ? ship : null;
    }

    /// <summary>
    /// Action 0, scrap: the ship leaves its fleet with its upkeep and is
    /// gone. Nothing is refunded. An emptied fleet stays until the star step
    /// clears it.
    /// </summary>
    public void ScrapShip(Player player, int shipId)
    {
        if (Commanded(player, shipId) is { } ship)
        {
            ship.Fleet!.RemoveShip(ship, Tables.Tech); // Commanded only returns ships in a fleet
        }
    }

    /// <summary>
    /// Action 1, mark for refit: the ship gets a new ship of the design in
    /// the slot, built when the yard where its fleet circles has nothing to
    /// do (<see cref="StartRefit"/>). Quirk kept: the yard's owner is not
    /// checked here, so a mark at a foreign yard never starts. An older
    /// mark is replaced.
    /// </summary>
    public void MarkRefit(Player player, int shipId, int designSlot)
    {
        if (Commanded(player, shipId) is not { } ship || !AtShipyard(ship.Fleet!) || player.Designs[designSlot] is not { } design)
        {
            return;
        }

        // The fix: a refit is taken only where it can start, at the player's own yard.
        if (Fixed(Fixes.OwnYardRefits) && ship.Fleet!.OrbitCenter is Planet { Colony: { } yardColony } && yardColony.Owner != player)
        {
            return;
        }

        ship.Refit = NewShip(design);
    }

    /// <summary>The fleet circles a planet whose colony, of any owner, has a shipyard.</summary>
    public static bool AtShipyard(Fleet fleet)
    {
        return fleet.OrbitCenter is Planet { Colony: { } colony } && colony.FirstOfKind(TechInfo.ShipyardKind) is not null;
    }

    /// <summary>
    /// Designs a ship may be refitted to: the player's slots in order, the
    /// same hull size, and a newer design than the ship's (by design id;
    /// the field compared, +0x48, is presumed the id).
    /// </summary>
    public static IEnumerable<Design> RefitChoices(Player player, Design ship)
    {
        return player.Designs.OfType<Design>().Where(d => d.Type == ship.Type && ship.Id < d.Id);
    }

    /// <summary>A fleet's ships' average condition, in whole percent.</summary>
    public static int FleetCondition(Fleet fleet)
    {
        if (fleet.Ships.Count == 0)
        {
            return 0;
        }

        var sum = fleet.Ships.Sum(s => (double)s.ConditionPercent());
        return (int)(sum / fleet.Ships.Count);
    }
}
