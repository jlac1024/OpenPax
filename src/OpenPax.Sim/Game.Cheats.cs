using OpenPax.Sim.Galaxy;
using OpenPax.Sim.Players;

namespace OpenPax.Sim;

/// <summary>
/// The developers' cheats (the cheat command, executed by
/// ; docs/ui/developer-window.md). The shipped game cannot reach
/// them; the port offers them through its console once cheats are turned on.
/// </summary>
public sealed partial class Game
{
    private const double Plenty = 999999999.0;

    /// <summary>Applies a cheat for a player. Special Test only counts (the original wrote a sync dump file).</summary>
    public StepEvents ApplyCheat(Player player, Cheat cheat)
    {
        var events = StepEvents.None;
        switch (cheat)
        {
            case Cheat.GiveLotsOfMoney:
                player.Money = 900000000.0;
                break;
            case Cheat.BuildAll:
                BuildAll(player, ref events);
                break;
            case Cheat.KillPlayer:
                CheckEliminationAndVictory(player, true, ref events);
                break;
            case Cheat.ResearchAll:
                ResearchAll(player);
                break;
            case Cheat.EspionageSucceeded:
                // every mission carried out at once, and it succeeds.
                foreach (var mission in player.Missions.ToList())
                {
                    TryMission(mission, ref events, forced: true);
                    player.Missions.Remove(mission);
                }

                break;
            case Cheat.MoreShips when player.Colonies.Count > 0:
                MoreShips(player, player.Colonies[0].Planet);
                break;
            case Cheat.ColonizeSectors:
                ColonizeSectors(player, ref events);
                break;
            case Cheat.ExploreAll:
                Observe(player);
                break;
            case Cheat.PlanetCombat:
                PlanetCombat(player, ref events);
                break;
            case Cheat.FleetCombat:
                FleetCombat(player);
                break;
            case Cheat.SpecialTest:
                SyncTests++;
                break;
        }

        return events;
    }

    /// <summary>How many Special Tests were asked for (the original's sync dump counter).</summary>
    public int SyncTests { get; private set; }

    /// <summary>
    /// Build All ( kind 1): every colony full of people and given
    /// five of each structure it may have, then everything finished.
    /// </summary>
    private void BuildAll(Player player, ref StepEvents events)
    {
        int[] kinds = [TechInfo.ShipyardKind, TechInfo.TrainingBaseKind, TechInfo.MissileBaseKind, TechInfo.BattleStationKind, TechInfo.FighterBaseKind, TechInfo.MineFieldKind];
        foreach (var colony in player.Colonies.ToList())
        {
            SetColonyResource(colony, Colony.Population, 100000.0, ref events);
            foreach (var kind in kinds)
            {
                var have = colony.FirstOfKind(kind)?.Count ?? 0;
                for (var i = have; i < Structure.MaxCount && BestOfKind(player, kind) is >= 0 and var best; i++)
                {
                    AddStructure(colony, Tables.Tech(best));
                }
            }
        }

        FinishAll(player, ref events);
    }

    /// <summary>Every colony's work finished and every fleet supplied ( with 1:).</summary>
    private void FinishAll(Player player, ref StepEvents events)
    {
        foreach (var colony in player.Colonies.ToList())
        {
            BuildStructures(colony, Plenty, ref events);
            BuildShips(colony, Plenty);
            BuildImprovements(colony, Plenty);
        }

        foreach (var fleet in player.Fleets.ToList())
        {
            FleetUpkeep(fleet, true);
        }
    }

    /// <summary>
    /// Research All: the fields' techs learned and the fields
    /// cleared, then every other tech, each taken up as research takes one
    /// (best armor, so the largest hull, structures and designs).
    /// </summary>
    private void ResearchAll(Player player)
    {
        foreach (var slot in player.ResearchSlots)
        {
            if (slot.Tech != ResearchSlot.None)
            {
                var tech = Tables.Tech(slot.Tech);
                GiveTech(player, tech);
                OnTechLearned(player, tech);
                slot.Clear();
            }
        }

        foreach (var tech in Tables.Techs)
        {
            if (!player.KnownTechs.Contains(tech.Id))
            {
                GiveTech(player, tech);
                OnTechLearned(player, tech);
            }
        }
    }

    /// <summary>
    /// More Ships: a new fleet at the planet with a ship of
    /// every design, until it is full; a computer player skips scouts and
    /// colony ships (hulls 0 and 2).
    /// </summary>
    private Fleet MoreShips(Player player, Planet planet)
    {
        var fleet = CreateFleet(player, Universe.Stars.First(s => s.Planets.Contains(planet)), planet);
        foreach (var design in player.Designs)
        {
            if (design is null || (player.IsAi && design.Type is 0 or 2))
            {
                continue;
            }

            if (!fleet.AddShip(NewShip(design), Tables.Tech))
            {
                break;
            }
        }

        return fleet;
    }

    /// <summary>Colonize Sectors: every system shown, and a colony on its first free planet that is not a gas giant.</summary>
    private void ColonizeSectors(Player player, ref StepEvents events)
    {
        foreach (var star in Universe.Stars)
        {
            UpdateSight(star, player);
            if (star.Planets.FirstOrDefault(p => p.Colony is null && p.Class != 5) is { } planet)
            {
                Colonize(player, planet, 1, ref events);
            }
        }
    }

    /// <summary>
    /// Planet Combat: another player still in the game learns
    /// everything, finishes everything and gets a fighter base and a
    /// minefield at its first colony, built at once; the player sees all and
    /// a fleet of every design is put at that colony with orders to attack.
    /// </summary>
    private void PlanetCombat(Player player, ref StepEvents events)
    {
        if (Players.FirstOrDefault(p => p != player && !p.Eliminated) is not { Colonies.Count: > 0 } other)
        {
            return;
        }

        Observe(player);
        ResearchAll(other);
        FinishAll(other, ref events);
        var planet = other.Colonies[0].Planet;
        var colony = other.Colonies.First(c => c.Star == other.Colonies[0].Star);
        foreach (var kind in (int[])[TechInfo.FighterBaseKind, TechInfo.MineFieldKind])
        {
            if (BestOfKind(other, kind) is >= 0 and var best)
            {
                AddStructure(colony, Tables.Tech(best));
            }
        }

        foreach (var kind in (int[])[TechInfo.MineFieldKind, TechInfo.FighterBaseKind])
        {
            if (colony.FirstOfKind(kind) is { } s)
            {
                BuildStructure(s, 99999999.0);
            }
        }

        var fleet = MoreShips(player, planet);
        GiveOrder(fleet, Order.To(planet));
        fleet.MoveTo(planet.X, planet.Y);
    }

    /// <summary>Fleet Combat: a fleet of every design for the player and for another, both at the other's first colony, the player's ordered onto the other's.</summary>
    private void FleetCombat(Player player)
    {
        if (Players.FirstOrDefault(p => p != player) is not { Colonies.Count: > 0 } other)
        {
            return;
        }

        Observe(player);
        var planet = other.Colonies[0].Planet;
        var ours = MoreShips(player, planet);
        var theirs = MoreShips(other, planet);
        GiveOrder(ours, Order.To(theirs));
        ours.MoveTo(theirs.X, theirs.Y);
    }
}
