using OpenPax.Sim.Numerics;
using OpenPax.Sim.Players;
using OpenPax.Sim.Setup;

namespace OpenPax.Sim;

/// <summary>Advisors over time, and orders about them (docs/sim/advisors.md).</summary>
public sealed partial class Game
{
    /// <summary>Every 2000 ticks each governor may grow in one skill.</summary>
    private void RunAdvisorsTask()
    {
        foreach (var p in Players)
        {
            foreach (var advisor in p.Advisors.ToList())
            {
                GrowGovernor(p, advisor);
            }
        }
    }

    /// <summary>
    /// A skill drawn at random grows by 0.01 toward 0.25 (0.02 toward 0.3
    /// for politicians), with chance (n - 1) / n where n is the skill in hundredths.
    /// </summary>
    private void GrowGovernor(Player p, Advisor advisor)
    {
        if (!advisor.IsGovernor)
        {
            return;
        }

        var k = Random.Next(4);
        var n = X87.ToInt(advisor.Skills[k] * 100.0);
        var r = Random.Next(n);
        if ((double)r < 0.3)
        {
            return;
        }

        var politician = p.Species.Is(Species.Politician);
        var d = politician ? Math.Min(0.3 - advisor.Skills[k], 0.02) : Math.Min(0.25 - advisor.Skills[k], 0.01);
        if (d > 0.0)
        {
            advisor.Skills[k] = d + advisor.Skills[k];
        }
    }

    /// <summary>Fires a star's governor; the star gets a new one.</summary>
    public void FireGovernor(Player p, int advisorId)
    {
        if (p.Advisors.FirstOrDefault(a => a.Id == advisorId) is not { IsGovernor: true, Star: not Advisor.NoStar } governor)
        {
            return;
        }

        p.Advisors.Remove(governor);
        p.Advisors.Insert(0, PlayerCreation.NewAdvisor(this, p, Advisor.GovernorRole, governor.Star));
    }

    /// <summary>
    /// A governor becomes the advisor for a field (0 to 3), replacing the
    /// one there; its star gets a new governor. No cost.
    /// </summary>
    // action 1; also the computer player's promotion
    public void AppointGovernor(Player p, int advisorId, int role)
    {
        if (p.Advisors.FirstOrDefault(a => a.Id == advisorId) is not { IsGovernor: true, Star: not Advisor.NoStar } governor)
        {
            return;
        }

        if (p.Advisors.FirstOrDefault(a => a.Role == role && a.Star == Advisor.NoStar) is { } current)
        {
            p.Advisors.Remove(current);
        }

        var star = governor.Star;
        governor.Promote(role);
        p.Advisors.Insert(0, PlayerCreation.NewAdvisor(this, p, Advisor.GovernorRole, star));
    }

    /// <summary>Advisors turned by an eliminated player are loyal again.</summary>
    private void ForgetTurnedAdvisors(Player eliminated)
    {
        foreach (var p in Players)
        {
            foreach (var a in p.Advisors)
            {
                if (a.TurnedBy == eliminated)
                {
                    a.TurnedBy = null;
                }
            }
        }
    }
}
