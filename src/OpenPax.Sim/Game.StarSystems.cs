using OpenPax.Sim.Galaxy;
using OpenPax.Sim.Players;

namespace OpenPax.Sim;

/// <summary>The every-tick star system step (docs/sim/star-step.md).</summary>
public sealed partial class Game
{
    /// <summary>True once the victory check has ended the game (+0x9C96).</summary>
    public bool Over { get; internal set; }

    /// <summary>
    /// The player at this machine: the first human, or null in a game of
    /// computer players only.
    /// </summary>
    public Player? LocalPlayer => Players.FirstOrDefault(p => !p.IsAi);

    private void StepStarSystems(ref StepEvents events)
    {
        foreach (var star in Universe.Stars)
        {
            StepStarSystem(star, ref events);
        }
    }

    private void StepStarSystem(Star star, ref StepEvents events)
    {
        foreach (var planet in star.Planets)
        {
            planet.Orbit.Step();
        }

        // A copy: the original reads each next link before handling a
        // fleet, so a fleet that leaves (removed, or jumped away) does not
        // stop the walk. One that jumps to a later system is stepped again
        // there in the same tick, as in the original.
        foreach (var fleet in star.Fleets.ToList())
        {
            if (fleet.Ships.Count < 1)
            {
                RemoveFleet(fleet, ref events);
            }
            else if (!fleet.InCombat)
            {
                Encounter(star, fleet);
                MoveFleet(fleet, ref events);
            }
        }

        // Combat markers (+0xAC) live only while a battle runs, and no star
        // step runs during a battle (strategic time stands still), so their
        // orbit step never has anything to move.
    }

    /// <summary>The owner drops an empty fleet; every fleet aiming at or circling it lets go.</summary>
    private void RemoveFleet(Fleet fleet, ref StepEvents events)
    {
        var owner = fleet.Owner;
        if (!owner.Fleets.Remove(fleet))
        {
            return;
        }

        foreach (var other in Players.SelectMany(p => p.Fleets).ToList())
        {
            ForgetTarget(other, fleet);
        }

        // The original updates sight before the owner and targets let go; nothing in between reads it.
        var star = Universe.Stars[fleet.Star];
        star.Fleets.Remove(fleet);
        UpdateSight(star);
        CheckEliminationAndVictory(owner, false, ref events);
    }

    /// <summary>A fleet whose orders or orbit involve a vanished object drops all orders and stops.</summary>
    private void ForgetTarget(Fleet fleet, ISpaceObject gone)
    {
        if (fleet.Orders.Any(o => o.Target == gone) || fleet.OrbitCenter == gone)
        {
            ClearOrders(fleet);
            fleet.EnterOrbit(Random, null);
        }
    }

    /// <summary>
    /// Eliminates a player left with nothing, then ends the game if no pair
    /// of players still in is at a treaty level of 3 or below, or has none.
    /// </summary>
    private void CheckEliminationAndVictory(Player player, bool eliminate, ref StepEvents events)
    {
        if (eliminate)
        {
            Eliminate(player, ref events);
        }

        if (!player.Eliminated && player.Colonies.Count == 0 && player.Fleets.Count == 0)
        {
            Eliminate(player, ref events);
            eliminate = true;
        }

        if (Over)
        {
            return;
        }

        // Quirk kept: with no pair of players still in, nothing reopens the
        // game, so a lone player wins as soon as this check first runs.
        Over = true;
        foreach (var a in Players.Where(p => !p.Eliminated))
        {
            foreach (var b in Players.Where(p => !p.Eliminated && p != a))
            {
                var level = a.Treaties[b.Index].Level();
                if (level <= 3 || level == Treaty.None)
                {
                    Over = false;
                }
            }
        }

        if (Over)
        {
            foreach (var human in Players)
            {
                Raise(human, human.Eliminated ? StepEvents.Defeat : StepEvents.Victory);
            }
        }
        else if (eliminate)
        {
            Raise(player, StepEvents.Defeat);
            foreach (var other in Players.Where(p => p != player && !p.IsAi))
            {
                Raise(other, StepEvents.PlayerEliminated);
                Notify(new GameNotice(NoticeKind.PlayerEliminated, other, Other: player));
            }
        }
    }

    private void Eliminate(Player player, ref StepEvents events)
    {
        player.Eliminated = true;
        Happen(new ModEvent(ModEventKind.PlayerEliminated, player));
        foreach (var other in Players.Where(p => p != player))
        {
            SetTreaty(other, player, Treaty.None, Treaty.InForce, ref events);
            SetTreaty(player, other, Treaty.None, Treaty.InForce, ref events);
        }

        // for every other player: packages the player
        // offered and missions against it go. Pending treaty offers are
        // already gone with the treaties.
        foreach (var other in Players.Where(p => p != player))
        {
            other.Packages.RemoveAll(k => k.Proposer == player);
            other.Missions.RemoveAll(m => m.Target == player);
        }

        ForgetTurnedAdvisors(player);

        // and the +0x800 list: and the player's own
        // missions and the packages waiting for it.
        player.Missions.Clear();
        player.Packages.Clear();

        // Not ported yet: removing what the player still owns. Only a player
        // with no colonies and no fleets can be eliminated so far.
    }

    /// <summary>Writes a treaty slot from one player toward another, with its knock-on effects.</summary>
    internal void SetTreaty(Player from, Player toward, int state, int value, ref StepEvents events)
    {
        var treaty = from.Treaties[toward.Index];
        var old = state == Treaty.None ? Treaty.Off : treaty.Slot(state);
        treaty.Write(state, value);
        if (state == Treaty.None || old != value)
        {
            Happen(new ModEvent(ModEventKind.TreatyChanged, from, toward, State: state, Value: value));
        }

        // A Repair and Resupply treaty can bring supply to fleets already out.
        if (state == Treaty.Resupply && value == Treaty.InForce)
        {
            ResupplyAll();
        }

        // Quirk kept: only a direct change between off and in force counts,
        // so accepting a pending Share Information does not.
        if (state == Treaty.ShareInformation && ((value == Treaty.InForce && old == Treaty.Off) || (value == Treaty.Off && old == Treaty.InForce)))
        {
            foreach (var star in Universe.Stars)
            {
                UpdateSight(star);
            }
        }

        if (state == Treaty.Allied && value == Treaty.InForce)
        {
            CheckEliminationAndVictory(from, false, ref events);
        }

        // (virtual +0x14, the same for both player classes): only a notice for a human.
        if (!from.IsAi && !from.Eliminated)
        {
            if (state != Treaty.War && value == Treaty.OfferedToMe)
            {
                Raise(from, StepEvents.TreatyOffered);
            }
            else if (state == Treaty.War && value != old && toward != from)
            {
                Raise(from, StepEvents.WarDeclared);
                Notify(new GameNotice(NoticeKind.WarChanged, from, Other: toward));
            }
        }
    }

    /// <summary>Every fleet at a star that supplies its owner has its jump counts reset.</summary>
    private void ResupplyAll()
    {
        foreach (var fleet in Players.SelectMany(p => p.Fleets))
        {
            if (Supplies(Universe.Stars[fleet.Star], fleet.Owner))
            {
                Resupply(fleet);
            }
        }
    }

    /// <summary>
    /// A system supplies a player who has a colony in it, or whose state 3
    /// treaty partner has one.
    /// </summary>
    public bool Supplies(Star star, Player player)
    {
        bool HasColony(Player p) => star.Planets.Any(planet => planet.Owner == p);

        if (HasColony(player))
        {
            return true;
        }

        return Players.Any(p => p != player && player.Treaties[p.Index].Has(3) && HasColony(p));
    }

    private void Resupply(Fleet fleet)
    {
        foreach (var ship in fleet.Ships)
        {
            ship.JumpsSinceSupply = 0;
        }

        fleet.UpdateStats(Tables.Tech);
    }
}
