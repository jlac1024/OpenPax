using OpenPax.Sim.Combat;

namespace OpenPax.Sim;

/// <summary>
/// The battle fought step by step, one 40 ms step at a time
/// (docs/sim/combat-step.md), shown in the battle window.
/// </summary>
public sealed partial class Game
{
    private const int ThinkingSubSteps = 16;
    private const int SubSteps = 20;
    private const int QuietSteps = 1500;
    private const int StillSteps = 250;
    private const double CanMove = 0.001;

    /// <summary>
    /// Battles with a human side are fought step by step, as in the
    /// original; off, every battle is resolved at once (some tests use that).
    /// </summary>
    public bool FightStepByStep { get; set; } = true;

    /// <summary>
    /// One step of a battle with a human side. In each: a side
    /// thinks (sub-steps 0 to 15, one side each), sensors look (every 10),
    /// everything moves, weapons fire (every 5), hits land, then each thing's
    /// post-step; the sub-step goes on and the end is checked.
    /// </summary>
    internal void TacticalStep(Battle battle, ref StepEvents events)
    {
        ThinkStep(battle);
        var s = battle.SubStep;
        if (s % 10 == 0)
        {
            for (var p = 0; p < Battle.Players; p++)
            {
                foreach (var ship in battle.Ships[p].ToList())
                {
                    Scan(battle, ship);
                }

                foreach (var orbital in battle.Orbitals[p].ToList())
                {
                    ScanOrbital(battle, orbital);
                }

                foreach (var mine in battle.Mines[p].ToList())
                {
                    ScanMine(battle, mine);
                }
            }
        }

        // shots, then missiles, then ships.
        foreach (var lists in (List<Shot>[][])[battle.Shots, battle.Missiles])
        {
            for (var p = 0; p < Battle.Players; p++)
            {
                foreach (var shot in lists[p].ToList())
                {
                    if (!shot.Dead && lists[p].Contains(shot))
                    {
                        MoveShot(battle, shot, lists[p], ref events);
                    }
                }
            }
        }

        for (var p = 0; p < Battle.Players; p++)
        {
            foreach (var ship in battle.Ships[p].ToList())
            {
                if (!ship.Dead)
                {
                    MoveShip(battle, ship);
                }
            }
        }

        for (var p = 0; p < Battle.Players; p++)
        {
            foreach (var mine in battle.Mines[p].ToList())
            {
                if (!mine.Dead)
                {
                    MoveMine(battle, mine);
                }
            }
        }

        for (var p = 0; p < Battle.Players; p++)
        {
            foreach (var fighter in battle.Fighters[p].ToList())
            {
                if (!fighter.Dead && battle.Fighters[p].Contains(fighter))
                {
                    MoveFighter(battle, fighter);
                }
            }
        }

        MoveEffects(battle);

        if (s % 5 == 0)
        {
            for (var p = 0; p < Battle.Players; p++)
            {
                foreach (var ship in battle.Ships[p].ToList())
                {
                    FireShip(battle, ship);
                }

                foreach (var orbital in battle.Orbitals[p].ToList())
                {
                    FireOrbital(battle, orbital);
                }

                foreach (var fighter in battle.Fighters[p].ToList())
                {
                    if (!fighter.Dead && battle.Fighters[p].Contains(fighter))
                    {
                        FireFighter(battle, fighter);
                    }
                }
            }
        }

        // missiles struck by point defense, mines, fighters, then the
        // planet's blocks.
        for (var p = 0; p < Battle.Players; p++)
        {
            foreach (var ship in battle.Ships[p].ToList())
            {
                ResolveShipHits(battle, ship, ref events);
            }
        }

        for (var p = 0; p < Battle.Players; p++)
        {
            foreach (var orbital in battle.Orbitals[p].ToList())
            {
                ResolveOrbitalHits(battle, orbital, ref events);
            }
        }

        for (var p = 0; p < Battle.Players; p++)
        {
            foreach (var missile in battle.Missiles[p].ToList())
            {
                InterceptMissile(battle, missile);
            }
        }

        for (var p = 0; p < Battle.Players; p++)
        {
            foreach (var mine in battle.Mines[p].ToList())
            {
                ResolveMineHits(battle, mine, ref events);
            }
        }

        for (var p = 0; p < Battle.Players; p++)
        {
            foreach (var fighter in battle.Fighters[p].ToList())
            {
                ResolveFighterHits(battle, fighter);
            }
        }

        foreach (var block in battle.Blocks)
        {
            ResolveBlockHits(battle, block, ref events);
        }

        TacticalPostStep(battle);
        battle.SubStep++;
        if (battle.SubStep == SubSteps)
        {
            battle.SubStep = 0;
        }

        if (TacticalBattleOver(battle))
        {
            EndBattle(battle, ref events);
        }
    }

    /// <summary>Each thing's post-step, player by player.</summary>
    private void TacticalPostStep(Battle battle)
    {
        for (var p = 0; p < Battle.Players; p++)
        {
            foreach (var ship in battle.Ships[p].ToList())
            {
                PostStepShip(battle, ship);
            }

            foreach (var orbital in battle.Orbitals[p])
            {
                PostStepOrbital(orbital);
            }

            foreach (var shot in battle.Shots[p].ToList())
            {
                PostStepShot(battle, shot, battle.Shots[p]);
            }

            foreach (var missile in battle.Missiles[p].ToList())
            {
                PostStepShot(battle, missile, battle.Missiles[p]);
            }

            foreach (var mine in battle.Mines[p].ToList())
            {
                PostStepMine(mine);
            }

            foreach (var fighter in battle.Fighters[p].ToList())
            {
                if (!fighter.Dead && battle.Fighters[p].Contains(fighter))
                {
                    PostStepFighter(battle, fighter);
                }
            }

            BlocksFall(battle);
        }

        PostStepEffects(battle);
    }

    /// <summary>
    /// A ship's post-step: followed enemies dead or
    /// out of sensor range are dropped; a dead target is let go (its state
    /// stays; a ship facing it goes idle at its next move); a goal that
    /// followed something now dead goes, and the ship stops; the shield
    /// regains some of what it soaked. A dead ship is dropped
    /// after its third post-step.
    /// </summary>
    private static void PostStepShip(Battle battle, BattleShip ship)
    {
        if (ship.Dead)
        {
            ship.DeathSteps++;
            if (ship.DeathSteps > 2)
            {
                battle.Ships[ship.Owner].Remove(ship);
            }

            return;
        }

        ship.Tracked.RemoveAll(o => o.Dead || DistanceD(o.PosX, o.PosY, ship.PosX, ship.PosY) > ship.SensorRange);
        if (ship.Target is { Dead: true })
        {
            ship.Target = null;
        }

        if (ship.Goal?.Target is { Dead: true })
        {
            ship.Detour = null;
            ship.Goal = null;
            ship.Speed = 0.0;
            ship.State = BattleShip.Idle;
        }

        ship.Ship.ShieldRegain();
    }

    /// <summary>
    /// Whether the battle is over. Quiet: with any
    /// ship able to move, 1500 steps after the last damaging hit and once no
    /// ship is moving; with none, 250 steps after the last shot. Then every
    /// ship gains experience and its fleet is let go, still retreating if it
    /// was. Otherwise as when resolving at once: fewer than two sides left.
    /// </summary>
    private bool TacticalBattleOver(Battle battle)
    {
        if (Quiet(battle))
        {
            GiveExperience(battle);
            foreach (var ship in Enumerable.Range(0, Battle.Players).SelectMany(p => battle.Ships[p]).ToList())
            {
                Release(ship.Fleet);
            }

            return true;
        }

        return OneSideLeft(battle);
    }

    private static bool Quiet(Battle battle)
    {
        var ships = Enumerable.Range(0, Battle.Players).SelectMany(p => battle.Ships[p]).ToList();
        if (battle.Steps <= battle.LastHit + QuietSteps)
        {
            foreach (var ship in ships)
            {
                if (ship.TopSpeed > CanMove || battle.Steps < battle.LastShot + StillSteps)
                {
                    return false;
                }
            }

            // Fighters out keep it going too.
            return !battle.Fighters.Any(l => l.Any(f => f.State != Fighter.DockedState));
        }

        return ships.All(s => s.Speed <= 0.0);
    }
}
