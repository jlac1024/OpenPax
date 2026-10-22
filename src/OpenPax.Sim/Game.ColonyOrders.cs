using OpenPax.Sim.Galaxy;
using OpenPax.Sim.Players;
using OpenPax.Sim.Setup;

namespace OpenPax.Sim;

/// <summary>
/// A player's orders to a colony from the Sector Display: the colony
/// command (type 0, run). Every op needs a colony on the
/// planet and the planet not in battle.
/// </summary>
public sealed partial class Game
{
    private static Colony? Ordered(Planet planet)
    {
        return planet.Colony is { } colony && !planet.InCombat ? colony : null;
    }

    /// <summary>
    /// Op 0, ships: each of the count goes to the front of the colony's first
    /// shipyard's queue, whatever its health ( kind 1).
    /// </summary>
    public void OrderShips(Player player, Planet planet, int designSlot, int count)
    {
        if (Ordered(planet)?.FirstOfKind(TechInfo.ShipyardKind) is not { } yard || player.Designs[designSlot] is not { } design)
        {
            return;
        }

        for (var i = 0; i < count; i++)
        {
            QueueShip(yard, NewShip(design));
        }
    }

    /// <summary>Op 0, a structure: a new unit or stack of the tech, if the kind limits allow it ( kind 2).</summary>
    public void OrderOrbital(Planet planet, int tech)
    {
        if (Ordered(planet) is { } colony && tech > 0)
        {
            AddStructure(colony, Tables.Tech(tech));
        }
    }

    /// <summary>Op 2, scrap: one unit of the first stack of a kind goes, with its upkeep. Nothing is refunded.</summary>
    public void Scrap(Planet planet, int kind)
    {
        if (Ordered(planet)?.FirstOfKind(kind) is { } stack)
        {
            RemoveStructureUnit(stack);
        }
    }

    /// <summary>
    /// Op 3, stop: the oldest ship order goes from the first shipyard's queue
    /// (a refit's old ship returns to a fleet at the planet); with none, one
    /// unit of the first structure below full health goes. Quirk kept: that
    /// also scraps a unit only damaged in battle. Work spent is lost.
    /// </summary>
    public void StopWork(Planet planet)
    {
        if (Ordered(planet) is not { } colony)
        {
            return;
        }

        if (colony.FirstOfKind(TechInfo.ShipyardKind) is { Queue.Count: > 0 } yard)
        {
            // the tail is the oldest order.
            var oldest = yard.Queue[^1];
            yard.Queue.RemoveAt(yard.Queue.Count - 1);
            if (yard.Queue.Count == 0)
            {
                yard.Progress = 0.0;
                yard.Total = 0.0;
            }

            if (oldest.Refit is { } old)
            {
                oldest.Refit = null;
                old.Refit = null;
                ShipyardFleet(colony).AddShip(old, Tables.Tech);
            }

            return;
        }

        // The fix: only a stack still being built loses a unit, not one damaged in battle.
        var stopped = Fixed(Fixes.ScrapUnderWork) ? colony.Structures.FirstOrDefault(s => s.UnderWork && s.Percent < 100.0) : Damaged(colony);
        if (stopped is { } damaged)
        {
            RemoveStructureUnit(damaged);
        }
    }

    /// <summary>
    /// Op 1, abandon: a nomad species first leaves in colony ships, one per
    /// ten population plus one, in a new fleet; then the colony is destroyed,
    /// which can eliminate the player and end the game.
    /// </summary>
    public StepEvents Abandon(Planet planet)
    {
        var events = StepEvents.None;
        if (Ordered(planet) is not { } colony)
        {
            return events;
        }

        var owner = colony.Owner;
        if (owner.Species.Is(Species.Nomad))
        {
            var count = (int)((colony.CurrentPopulation * 0.1) - -1.0);
            var fleet = CreateFleet(owner, Universe.Stars[colony.Star], planet);
            fleet.Name = Tables.AbandonFleetName;
            var design = ColonyDesign(owner);
            for (var i = 0; i < count && design is not null; i++)
            {
                // a ship past 21 is refused, as the original
                // refuses it; its id is used and its notice still goes out.
                var ship = NewShip(design);
                fleet.AddShip(ship, Tables.Tech);

                // Each ship made is told as one finished at a yard.
                Notify(new GameNotice(NoticeKind.ShipFinished, owner, Place: planet, Ship: ship));
                Happen(new ModEvent(ModEventKind.ShipBuilt, owner, Place: planet, Ship: ship));
            }
        }

        DestroyColony(colony, ref events);
        return events;
    }

    /// <summary>The first design carrying a colonizer, or a "Transport" made for the purpose.</summary>
    private Design? ColonyDesign(Player owner)
    {
        Design? Find() => owner.Designs.FirstOrDefault(d => d is not null && d.Specials.Any(c => c.Tech.Kind == TechInfo.ColonizerKind));

        if (Find() is { } found)
        {
            return found;
        }

        var design = new Design(Tables.Hulls[2]) { Owner = owner };
        if (owner.BestArmor > 0)
        {
            design.Add(Tables.Tech(owner.BestArmor), Component.NotWeapon);
        }

        design.Name = Tables.TransportName;
        DesignBuilder.Build(this, 2, design, owner);
        PlayerCreation.AddDesign(this, owner, design);
        return Find();
    }

    /// <summary>Where a finished or returned ship goes: a human's fleet circling the planet with room, else a new fleet.</summary>
    private Fleet ShipyardFleet(Colony colony)
    {
        var owner = colony.Owner;
        var star = Universe.Stars[colony.Star];
        var fleet = owner.IsAi ? null
            : star.Fleets.FirstOrDefault(f => f.Owner == owner && f.OrbitCenter == colony.Planet && f.Ships.Count < Fleet.MaxShips);
        return fleet ?? CreateFleet(owner, star, colony.Planet);
    }

    /// <summary>The colony's first shipyard has a ship in its queue.</summary>
    public static bool ShipInBuild(Colony colony)
    {
        return colony.FirstOfKind(TechInfo.ShipyardKind) is { Queue.Count: > 0 };
    }

    /// <summary>The build button for a structure kind would be offered (the six predicates).</summary>
    public bool CanBuildOrbital(Colony colony, int kind)
    {
        return StructureAllowed(colony, kind);
    }

    /// <summary>The best known tech of a structure kind, or -1.</summary>
    public int BestOrbital(Player player, int kind)
    {
        return BestOfKind(player, kind);
    }
}
