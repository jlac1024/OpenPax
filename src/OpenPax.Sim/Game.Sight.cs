using OpenPax.Sim.Galaxy;
using OpenPax.Sim.Players;

namespace OpenPax.Sim;

/// <summary>What each player sees of each star system (docs/sim/visibility.md).</summary>
public sealed partial class Game
{
    /// <summary>
    /// "Observe Game" after the end (the command, kind 7,
    /// ): every system is shown to the player, who goes on watching.
    /// </summary>
    public void Observe(Player player)
    {
        foreach (var star in Universe.Stars)
        {
            UpdateSight(star, player);
        }
    }

    /// <summary>
    /// Works out every player's sight of a system after something there
    /// changed, and players present there meet. A player shown the system
    /// some other way (espionage, trade) first remembers it. No draws.
    /// </summary>
    internal void UpdateSight(Star star, Player? shownTo = null)
    {
        if (shownTo is not null)
        {
            SetSight(star, shownTo, Star.Remembered);
        }

        var present = Players.Select(p => Present(star, p)).ToArray();

        for (var i = 0; i < Players.Count; i++)
        {
            if (present[i])
            {
                SetSight(star, Players[i], Star.InView);
            }
            else if (star.Sight[i] == Star.InView)
            {
                SetSight(star, Players[i], Star.Remembered);
            }
        }

        // Share Information shows a partner's view, but
        // only the lower index's treaty is read, for both directions.
        for (var i = 0; i < Players.Count; i++)
        {
            for (var j = i + 1; j < Players.Count; j++)
            {
                if (Players[i].Treaties[j].Slot(Treaty.ShareInformation) != Treaty.InForce)
                {
                    continue;
                }

                if (present[j])
                {
                    SetSight(star, Players[i], Star.InView);
                }

                if (present[i])
                {
                    SetSight(star, Players[j], Star.InView);
                }
            }
        }

        for (var i = 0; i < Players.Count; i++)
        {
            for (var j = i + 1; j < Players.Count && present[i]; j++)
            {
                if (present[j] && Players[i].Treaties[j].Level() == Treaty.None)
                {
                    // Neutral in force raises no step events; the original's
                    // "you have met" notice (virtual +0x84) is the host's job.
                    var events = StepEvents.None;
                    SetTreaty(Players[i], Players[j], Treaty.Neutral, Treaty.InForce, ref events);
                    SetTreaty(Players[j], Players[i], Treaty.Neutral, Treaty.InForce, ref events);
                    Met(Players[i], Players[j]);
                }
            }
        }
    }

    /// <summary>Each side is told it has met the other (player virtual +0x84).</summary>
    private void Met(Player a, Player b)
    {
        Notify(new GameNotice(NoticeKind.FirstContact, a, Other: b));
        Notify(new GameNotice(NoticeKind.FirstContact, b, Other: a));
    }

    /// <summary>
    /// Sets a player's sight of a system. Psychic species and eliminated
    /// players always get full view, whatever was asked.
    /// </summary>
    private static void SetSight(Star star, Player player, int sight)
    {
        if (player.Species.Has(Species.Psychic) || player.Eliminated)
        {
            sight = Star.InView;
        }

        star.Sight[player.Index] = sight;
    }

    /// <summary>A player still in the game with a colony or fleet in the system.</summary>
    private static bool Present(Star star, Player player)
    {
        return !player.Eliminated && (star.Planets.Any(p => p.Owner == player) || star.Fleets.Any(f => f.Owner == player));
    }
}
