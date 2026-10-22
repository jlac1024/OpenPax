using OpenPax.Sim.Players;

namespace OpenPax.Sim;

/// <summary>Attitudes, incidents and treaty offers (docs/sim/diplomacy.md).</summary>
public sealed partial class Game
{
    private const double RespectRate = 0.05;

    /// <summary>
    /// Every 200 ticks each player warms to every player it has met that is
    /// stronger per colony, and drifts by its fixed disposition toward them.
    /// </summary>
    private void RunDiplomacyTask()
    {
        foreach (var p in Players)
        {
            foreach (var q in Players)
            {
                if (p == q || p.Treaties[q.Index].Level() == Treaty.None)
                {
                    continue;
                }

                var gap = (AverageStrength(q) - AverageStrength(p)) * RespectRate;
                if (gap > 0.0)
                {
                    Incident(q, p, IncidentKind.Stronger, gap);
                }

                Incident(q, p, IncidentKind.Disposition, p.Relations[q.Index]);
            }
        }
    }

    /// <summary>Colony and fleet strength over the number of colonies; 0 with none.</summary>
    internal double AverageStrength(Player p)
    {
        var sum = 0.0;
        foreach (var c in p.Colonies)
        {
            sum = ColonyStrength(c) + sum;
        }

        foreach (var f in p.Fleets)
        {
            sum = f.Strength + sum;
        }

        return p.Colonies.Count == 0 ? 0.0 : sum / p.Colonies.Count;
    }

    /// <summary>
    /// Something one player did to another: <paramref name="b"/>'s score
    /// toward <paramref name="a"/> moves, and for some kinds a's toward b too.
    /// </summary>
    internal static void Incident(Player a, Player b, IncidentKind kind, double amount = 0.0)
    {
        switch (kind)
        {
            case IncidentKind.TradeRoutes or IncidentKind.JointResearch:
                AddScore(b, a, 0.1);
                break;
            case IncidentKind.Disposition or IncidentKind.Stronger:
            case IncidentKind.ShipsGiven or IncidentKind.PlanetsGiven or IncidentKind.TechsGiven
                or IncidentKind.CreditsGiven or IncidentKind.MapGiven:
                AddScore(b, a, amount);
                break;
            case IncidentKind.ShipsReceived or IncidentKind.PlanetsReceived or IncidentKind.TechsReceived
                or IncidentKind.CreditsReceived or IncidentKind.MapReceived:
                AddScore(a, b, amount);
                break;
            case IncidentKind.EnemyAttacked:
                AddScore(b, a, 20.0);
                break;
            case IncidentKind.FriendAttacked:
                AddScore(b, a, -20.0);
                break;
            case IncidentKind.SpyCaught:
                AddScore(b, a, -5.0);
                break;
            case IncidentKind.ColonyTaken:
                AddScore(a, b, amount);
                AddScore(b, a, -amount);
                break;
            case IncidentKind.ShipDestroyed:
                AddScore(a, b, 2.0);
                AddScore(b, a, -5.0);
                break;
            case IncidentKind.StructureDestroyed:
                AddScore(a, b, 1.0);
                AddScore(b, a, -3.0);
                break;
            case IncidentKind.PlanetAttacked:
                AddScore(b, a, -40.0);
                break;
            case IncidentKind.FleetAttacked:
                AddScore(b, a, -20.0);
                break;
            case IncidentKind.TradeRejected:
                AddScore(a, b, -5.0);
                AddScore(b, a, -5.0);
                break;
        }
    }

    /// <summary>A score toward another player moves, within 250 either way; exactly 0 becomes +1 or -1.</summary>
    private static void AddScore(Player p, Player toward, double amount)
    {
        var v = Math.Max(Math.Min(p.RelationScores[toward.Index] + amount, 250.0), -250.0);
        if (v == 0.0)
        {
            v = p.Relations[toward.Index] > 0 ? 0.0 - -1.0 : 0.0 - 1.0;
        }

        p.RelationScores[toward.Index] = v;
    }

    /// <summary>
    /// Others react to an attack: players at war with the victim warm to the
    /// attacker, the victim's allies (level above 3) cool. The victim's view of the levels.
    /// </summary>
    private void ReactToAttack(Player attacker, Player victim)
    {
        foreach (var x in Players)
        {
            if (x == victim)
            {
                continue;
            }

            var level = victim.Treaties[x.Index].Level();
            // Quirk kept: a player the victim never met (200) counts as above 3; not with the fix.
            if (level > 3 && x != attacker && !(level == Treaty.None && Fixed(Fixes.StrangersIndifferent)))
            {
                Incident(attacker, x, IncidentKind.FriendAttacked);
            }

            if (level < 1 && x != attacker)
            {
                Incident(attacker, x, IncidentKind.EnemyAttacked);
            }
        }
    }

    /// <summary>Offers a treaty state: pending on both sides, answered later by the other player.</summary>
    // (offer message, run at once in single player)
    public StepEvents OfferTreaty(Player from, Player to, int state)
    {
        var events = StepEvents.None;
        if (state is >= 0 and < Treaty.States)
        {
            SetTreaty(from, to, state, Treaty.OfferedByMe, ref events);
            SetTreaty(to, from, state, Treaty.OfferedToMe, ref events);
        }

        return events;
    }

    /// <summary>
    /// Sets a treaty state on both sides at once: accepting or turning down
    /// an offer, declaring war, canceling, withdrawing. The other side is written first.
    /// </summary>
    public StepEvents ReplyTreaty(Player me, Player other, int state, int value)
    {
        var events = StepEvents.None;
        if (state is >= 0 and < Treaty.States)
        {
            SetTreaty(other, me, state, value, ref events);
            SetTreaty(me, other, state, value, ref events);
        }

        return events;
    }
}
