using OpenPax.Sim;
using OpenPax.Sim.Players;

namespace OpenPax.Ai;

/// <summary>
/// A computer player's espionage (+0x10D4): with espionage to spend and
/// no mission running, it starts one against a player it has met and is
/// not allied with. The kinds allowed depend on how friendly they are.
/// </summary>
internal static class Espionage
{
    public static void Run(Game game, Player p)
    {
        if (!(0.0 < p.EspionagePoints * 10.0) || p.Missions.Count > 0)
        {
            return;
        }

        while (PickTarget(game, p) is { } target)
        {
            var level = game.Random.Next(SpyMission.Levels);
            if (TryStart(game, p, target, level))
            {
                return;
            }
        }
    }

    /// <summary>A player at random still in the game, met and below alliance; none when there is no such player.</summary>
    private static Player? PickTarget(Game game, Player p)
    {
        bool Fits(Player q)
        {
            var level = p.Treaties[q.Index].Level();
            return q != p && !q.Eliminated && level != Treaty.None && level < Treaty.Allied;
        }

        if (!game.Players.Any(Fits))
        {
            return null;
        }

        while (true)
        {
            var q = game.Players[game.Random.Next(game.Players.Count)];
            if (Fits(q))
            {
                return q;
            }
        }
    }

    /// <summary>
    /// Draws a kind and starts it if the treaty level allows: ships and
    /// colonies only at war or neutral (stealing ships only at war), the
    /// advisors and techs up to resupply, killing advisors up to neutral.
    /// Revealing systems is never chosen.
    /// </summary>
    private static bool TryStart(Game game, Player p, Player target, int level)
    {
        var treaty = p.Treaties[target.Index].Level();
        var kind = game.Random.Next(SpyMission.Kinds);
        var allowed = kind switch
        {
            SpyMission.StealShips => target.Fleets.Count > 0 && treaty <= Treaty.War,
            SpyMission.DestroyShips => target.Fleets.Count > 0 && treaty <= Treaty.Neutral,
            SpyMission.Sabotage or SpyMission.LocateColony => target.Colonies.Count > 0 && treaty <= Treaty.Neutral,
            SpyMission.TurnAdvisors => treaty <= Treaty.Resupply && target.Advisors.Any(a => !a.IsGovernor && a.TurnedBy != p),
            SpyMission.TurnGovernors => treaty <= Treaty.Resupply && target.Advisors.Any(a => a.IsGovernor && a.TurnedBy != p),
            SpyMission.Assassinate => treaty <= Treaty.Neutral && target.Advisors.Any(a => !a.IsGovernor && a.TurnedBy != p),
            SpyMission.StealTechs => treaty <= Treaty.Resupply,
            _ => false,
        };

        if (allowed)
        {
            game.AddMission(p, target, kind, level);
        }

        return allowed;
    }
}
