using OpenPax.Sim.Galaxy;
using OpenPax.Sim.Players;
using OpenPax.Sim.Setup;

namespace OpenPax.Sim;

/// <summary>Founding, changing and losing colonies (docs/sim/economy.md).</summary>
public sealed partial class Game
{
    private const double InstantBuild = 99999999.0;

    /// <summary>
    /// Founds a colony at a colonizer generation (1 at setup). Generations 2
    /// and 3 start bigger, and 3 with a missile base.
    /// </summary>
    internal Colony Colonize(Player player, Planet planet, int generation, ref StepEvents events)
    {
        var star = Universe.Stars.First(s => s.Planets.Contains(planet));
        var colony = new Colony(player, planet, star.Index, Tables.OfKind(TechInfo.ImprovementKind).Count());
        player.Colonies.Insert(0, colony);
        planet.Colony = colony;
        planet.Owner = player;
        Happen(new ModEvent(ModEventKind.ColonyFounded, player, Place: planet));

        // a slot for every improvement the player knows.
        foreach (var tech in Tables.OfKind(TechInfo.ImprovementKind))
        {
            if (player.KnownTechs.Contains(tech.Id))
            {
                colony.Improvements[ImprovementSlot(tech)] = new Improvement(tech);
            }
        }

        if (!player.Advisors.Any(a => a.Role == Advisor.GovernorRole && a.Star == star.Index))
        {
            player.Advisors.Insert(0, PlayerCreation.NewAdvisor(this, player, Advisor.GovernorRole, star.Index));
        }

        if (generation is 2 or 3)
        {
            SetColonyResource(colony, Colony.Population, generation == 2 ? 3.0 : 5.0, ref events);
            if (generation == 3)
            {
                var missileBase = PlayerCreation.BestKnown(player, Tables, TechInfo.MissileBaseKind);
                AddStructure(colony, Tables.Tech(missileBase));
                if (colony.FirstOfKind(TechInfo.MissileBaseKind) is { } built)
                {
                    BuildStructure(built, InstantBuild);
                }
            }

            CompleteFirstConstructionImprovement(colony);
        }

        UpdateSight(star);
        ResupplyAll();
        FirstContact(star, player, ref events);
        return colony;
    }

    /// <summary>The improvement's place among the improvements.</summary>
    private int ImprovementSlot(TechInfo tech)
    {
        return Tables.IndexInKind(tech);
    }

    /// <summary>Completes the first improvement, known or not, with a flat construction value.</summary>
    private void CompleteFirstConstructionImprovement(Colony colony)
    {
        var tech = Tables.OfKind(TechInfo.ImprovementKind).FirstOrDefault(t => t.Flat[Colony.Construction] != 0.0);
        if (tech is not null && colony.Improvements[ImprovementSlot(tech)] is { } improvement)
        {
            BuildImprovement(colony, improvement, 999999.0);
        }
    }

    /// <summary>
    /// Players with a colony or fleet one wormhole away and no treaty with
    /// the founder meet it: treaty state 1 both ways.
    /// </summary>
    private void FirstContact(Star star, Player player, ref StepEvents events)
    {
        foreach (var hole in star.Wormholes)
        {
            if (hole.Partner is not { } partner)
            {
                continue;
            }

            var neighbor = Universe.Stars[partner.Star];
            foreach (var other in Players)
            {
                var present = !other.Eliminated
                    && (neighbor.Planets.Any(p => p.Owner == other) || neighbor.Fleets.Any(f => f.Owner == other));
                if (other != player && present && player.Treaties[other.Index].Level() == Treaty.None)
                {
                    SetTreaty(player, other, Treaty.Neutral, Treaty.InForce, ref events);
                    SetTreaty(other, player, Treaty.Neutral, Treaty.InForce, ref events);
                    Met(player, other);
                }
            }
        }
    }

    /// <summary>
    /// Orders a unit of a structure, if the kind limits allow it: onto the
    /// stack of that kind (which keeps its own tech), else as a new stack
    /// at the front. The unit is built by construction.
    /// </summary>
    internal void AddStructure(Colony colony, TechInfo tech)
    {
        if (!StructureAllowed(colony, tech.Kind))
        {
            return;
        }

        var structure = colony.FirstOfKind(tech.Kind);
        if (structure is not null)
        {
            if (structure.Count + 1 < Structure.MaxCount + 1)
            {
                structure.Count++;
                structure.UnderWork = true;
            }
        }
        else
        {
            structure = new Structure(tech, colony) { UnderWork = true };
            colony.Structures.Insert(0, structure);

            foreach (var known in Tables.Techs)
            {
                if (colony.Owner.KnownTechs.Contains(known.Id))
                {
                    FitStructure(structure, known);
                }
            }
        }

        var upkeep = (double)TechLevels.Cost(structure.Tech, colony.Owner);
        colony.Owner.StructureUpkeep[StructureBucket(structure.Tech.Kind)] += upkeep;
        colony.StructureUpkeep += upkeep;
    }

    /// <summary>
    /// Whether another unit of a kind may go at a colony: fewer than 5 and
    /// fewer than the best known tech's level (minefields have no level
    /// limit), with the weapons each kind needs known.
    /// </summary>
    internal bool StructureAllowed(Colony colony, int kind)
    {
        var p = colony.Owner;
        var count = colony.FirstOfKind(kind)?.Count ?? 0;
        var best = BestOfKind(p, kind);
        var level = best < 0 ? -1 : Tables.Tech(best).InstallationLevel;
        var belowLevel = count < Structure.MaxCount && count < level;
        return kind switch
        {
            TechInfo.MissileBaseKind => belowLevel && Knows(p, kind) && Knows(p, TechInfo.MissileKind),
            9 => belowLevel && Knows(p, 9) && Knows(p, TechInfo.FighterKind),
            TechInfo.BattleStationKind => belowLevel && Knows(p, kind)
                && (Knows(p, TechInfo.EnergyWeaponKind) || Knows(p, TechInfo.ProjectileKind) || Knows(p, TechInfo.AreaWeaponKind)),
            10 => count < Structure.MaxCount && Knows(p, 10),
            TechInfo.ShipyardKind => belowLevel && Knows(p, kind),
            TechInfo.TrainingBaseKind => !p.Species.Is(Species.Warrior) && belowLevel && Knows(p, kind),
            _ => false,
        };
    }

    /// <summary>The highest-id known tech of a kind, or -1.</summary>
    public int BestOfKind(Player p, int kind)
    {
        return Tables.OfKind(kind).LastOrDefault(t => p.KnownTechs.Contains(t.Id))?.Id ?? -1;
    }

    /// <summary>Whether the player knows any tech of a kind.</summary>
    public bool Knows(Player p, int kind)
    {
        return Tables.OfKind(kind).Any(t => p.KnownTechs.Contains(t.Id));
    }

    private static int StructureBucket(int kind)
    {
        return kind switch
        {
            TechInfo.ShipyardKind => 0,
            TechInfo.TrainingBaseKind => 1,
            _ => 2,
        };
    }

    /// <summary>Takes one unit off a stack, with its upkeep; the last unit takes the stack away.</summary>
    private static void RemoveStructureUnit(Structure structure)
    {
        var colony = structure.Colony;
        if (structure.Count > 1)
        {
            structure.Count--;
            structure.HitPoints = Math.Min(structure.HitPoints, structure.MaxHitPoints);
        }
        else
        {
            colony.Structures.Remove(structure);
        }

        var upkeep = (double)TechLevels.Cost(structure.Tech, colony.Owner);
        colony.Owner.StructureUpkeep[StructureBucket(structure.Tech.Kind)] -= upkeep;
        colony.StructureUpkeep -= upkeep;
    }

    /// <summary>Adds (or, when scrapping, takes away) an improvement's effect on its one resource.</summary>
    private void ApplyImprovement(Colony colony, Improvement improvement, bool add)
    {
        var tech = improvement.Tech;
        var r = tech.Resource;
        var sign = add ? 1.0 : -1.0;
        colony.PerPopulation[r] = Math.Max(0.0, colony.PerPopulation[r] + sign * tech.PerPopulation[r]);
        colony.Flat[r] = Math.Max(0.0, colony.Flat[r] + sign * tech.Flat[r]);
        if (r == Colony.Population && colony.Flat[Colony.Population] != 0.0)
        {
            colony.MaxPopulation = Colony.MaximumPopulation(colony.Owner, colony.Planet, colony);
        }

        var upkeep = (double)TechLevels.Cost(tech, colony.Owner);
        if (upkeep > 0.0)
        {
            colony.Owner.ImprovementUpkeep[r] += sign * upkeep;
            if (r is >= 1 and <= 3)
            {
                colony.ImprovementUpkeep[r] += sign * upkeep;
            }
        }
    }

    /// <summary>
    /// Sets a colony resource: clamped at 0 except espionage, population
    /// capped at the maximum. A colony left with no population is destroyed.
    /// </summary>
    internal void SetColonyResource(Colony colony, int resource, double value, ref StepEvents events)
    {
        if (resource != Colony.Espionage && value < 0.0)
        {
            value = 0.0;
        }

        if (resource == Colony.Population)
        {
            value = Math.Min(value, Colony.MaximumPopulation(colony.Owner, colony.Planet, colony));
        }

        colony.Resources[resource] = value;
        if (colony.CurrentPopulation <= 0.0 && !colony.Destroyed)
        {
            DestroyColony(colony, ref events);
        }
    }

    /// <summary>The colony and all it holds go; the owner may be eliminated.</summary>
    private void DestroyColony(Colony colony, ref StepEvents events)
    {
        var owner = colony.Owner;
        colony.Destroyed = true;
        owner.Colonies.Remove(colony);
        owner.Funding = Math.Max(0.0, owner.Funding - colony.Funding);
        colony.Funding = 0.0;
        foreach (var improvement in colony.Improvements.OfType<Improvement>())
        {
            if (improvement.Complete)
            {
                ApplyImprovement(colony, improvement, false);
            }
        }

        while (colony.Structures.Count > 0)
        {
            RemoveStructureUnit(colony.Structures[0]);
        }

        colony.Planet.Colony = null;
        colony.Planet.Owner = null;

        UpdateSight(Universe.Stars[colony.Star]);
        if (!owner.Colonies.Any(c => c.Star == colony.Star))
        {
            owner.Advisors.RemoveAll(a => a.Role == Advisor.GovernorRole && a.Star == colony.Star);
        }

        Notify(new GameNotice(NoticeKind.ColonyLost, owner, Place: colony.Planet));
        Happen(new ModEvent(ModEventKind.ColonyDestroyed, owner, Place: colony.Planet));
        CheckEliminationAndVictory(owner, false, ref events);
    }
}
