using OpenPax.Sim.Combat;
using OpenPax.Sim.Players;

namespace OpenPax.Sim;

/// <summary>The planet's blocks under bombardment in a battle fought step by step (docs/sim/combat-step.md).</summary>
public sealed partial class Game
{
    /// <summary>The ruin each block leaves: its kind and its offset from the block's place.</summary>
    private static readonly (int Kind, int X, int Y)[] _blockRuins =
    [
        (141, -45, -55), (136, -55, -65), (139, -75, -140), (135, -50, -100), (136, -40, -62),
        (138, -40, -50), (140, -45, -95), (141, -50, -50), (142, -40, -60), (140, -50, -55),
    ];

    /// <summary>
    /// Bombs that reached a block land, each shot whole: the
    /// colony takes damage per round times rounds, and the shooter's player
    /// an incident for the population lost. The block falls once the colony
    /// has fewer bands than its index; else it shows a split. A colony
    /// destroyed takes every mine on the field with it. No draws.
    /// </summary>
    internal void ResolveBlockHits(Battle battle, PlanetBlock block, ref StepEvents events)
    {
        if (block.Dead)
        {
            return;
        }

        foreach (var h in block.Incoming)
        {
            if (!block.Dead && battle.Planet?.Colony is { } colony)
            {
                var x = h.RoundDamage() * h.Rounds;
                if (x != 0)
                {
                    battle.LastHit = battle.Steps;
                    var owner = colony.Owner;
                    var before = colony.CurrentPopulation;
                    DamageColony(colony, x, ref events);
                    var after = colony.Destroyed ? 0.0 : colony.CurrentPopulation;
                    if (before - after > 0.0 && h.Owner < Players.Count)
                    {
                        Incident(Players[h.Owner], owner, IncidentKind.ColonyTaken, before - after);
                    }

                    if (colony.Destroyed)
                    {
                        PurgeMines(battle);
                    }

                    battle.Bands = colony.Destroyed ? -1 : Bands(colony);
                    if (battle.Bands < block.Index)
                    {
                        BlockFalls(battle, block);
                    }
                    else
                    {
                        AddFollower(battle.OrbitalEffects, BandSplit, block, h.X - block.X - 25, h.Y - block.Y - 45);
                    }
                }
            }

            ShotDies(battle, h, explode: false);
        }

        block.Incoming.Clear();
    }

    /// <summary>A block falls to bombing: the planet object moves to it and its ruin rides there.</summary>
    private void BlockFalls(Battle battle, PlanetBlock block)
    {
        if (battle.PlanetObject is { } planet)
        {
            planet.SetPosition(block.X, block.Y);
            var ruin = _blockRuins[block.Index];
            AddFollower(battle.OrbitalEffects, ruin.Kind, planet, ruin.X, ruin.Y);
        }

        block.Dead = true;
    }
}
