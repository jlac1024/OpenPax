using OpenPax.Sim.Galaxy;
using OpenPax.Sim.Players;

namespace OpenPax.Sim.Setup;

/// <summary>
/// A player's starting colony, structures and fleets; see
/// docs/sim/players.md. Every draw is made in the original's order. Which
/// improvements the colony starts with is ported with the economy.
/// </summary>
internal static class StartingAssets
{
    private const int AiFleetDesignSlot = 2;
    private const int HumanColonyShipSlot = 2;
    private const int HumanScoutSlot = 0;

    // Colony ships in a human's first fleet by difficulty 1..5; at 5 it is empty.
    private static readonly int[] _humanColonyShips = [2, 2, 1, 1, 0];

    public static void Give(Game game, Player player, Star star, Planet planet, GameTables tables)
    {
        if (player.IsAi)
        {
            PlayerCreation.RebuildAiDesigns(game, player, null);
        }

        var events = StepEvents.None;
        var colony = game.Colonize(player, planet, 1, ref events);
        game.AddStructure(colony, tables.Tech(PlayerCreation.BestKnown(player, tables, TechInfo.ShipyardKind)));
        game.AddStructure(colony, tables.Tech(PlayerCreation.BestKnown(player, tables, TechInfo.MissileBaseKind)));

        if (player.IsAi)
        {
            for (var i = 0; i < game.Setup.Difficulty; i++)
            {
                AddFleet(game, player, star, planet, [AiFleetDesignSlot]);
            }
        }
        else
        {
            AddFleet(game, player, star, planet, [.. Enumerable.Repeat(HumanColonyShipSlot, _humanColonyShips[game.Setup.Difficulty - 1])]);
            AddFleet(game, player, star, planet, [HumanScoutSlot]);
        }

        // Everything known is finished at once, in plan order; then the
        // colony starts full.
        game.ConstructColony(colony, true, ref events);
        game.SetColonyResource(colony, Colony.Population, colony.MaxPopulation, ref events);
    }

    private static void AddFleet(Game game, Player player, Star star, Planet planet, int[] designSlots)
    {
        var fleet = game.CreateFleet(player, star, planet);
        foreach (var slot in designSlots)
        {
            // a copy of the design in the slot with its own id.
            if (player.Designs[slot] is { } design)
            {
                fleet.AddShip(game.NewShip(design), game.Tables.Tech);
            }
        }
    }
}
