using OpenPax.Sim.Players;

namespace OpenPax.Ai.Tests;

public class AdvisorTests
{
    [Fact]
    public void GovernorsGrowAndStayUnderTheirCap()
    {
        var game = AiGames.New(1234, 2, 3);
        var p = game.Players[0];
        var governor = p.Advisors.Single(a => a.IsGovernor);
        var before = governor.Skills.Sum();
        AiGames.Run(game, 40000);

        var now = p.Advisors.Where(a => a.IsGovernor).ToList();
        Assert.All(now, g => Assert.All(g.Skills, s => Assert.True(s <= 0.3)));
        Assert.True(now.Any(g => g.Skills.Sum() > before) || p.Advisors.Any(a => !a.IsGovernor && a.Skills.Max() > 0.12));
    }

    [Fact]
    public void ATurnedAdvisorWorksAgainstItsOwner()
    {
        var game = AiGames.New();
        var (p, spy) = (game.Players[0], game.Players[1]);
        var research = p.Advisors.Single(a => a.Role == 0);
        var value = research.Value(0);
        research.TurnedBy = spy;
        Assert.Equal(-value, research.Value(0));
    }

    [Fact]
    public void AComputerPlayerReplacesATurnedGovernor()
    {
        var game = AiGames.New();
        var (p, spy) = (game.Players[0], game.Players[1]);
        var governor = p.Advisors.Single(a => a.IsGovernor);
        governor.TurnedBy = spy;

        while (game.LastThinker != p.Index)
        {
            game.Step();
        }

        Assert.DoesNotContain(governor, p.Advisors);
        Assert.Single(p.Advisors, a => a.IsGovernor && a.Star == governor.Star && a.TurnedBy is null);
    }

    [Fact]
    public void AComputerPlayerPromotesAGovernorThatBeatsTheHolder()
    {
        var game = AiGames.New();
        var p = game.Players[0];
        var governor = p.Advisors.Single(a => a.IsGovernor);
        var star = governor.Star;
        governor.Skills[0] = 0.25;
        var holder = p.Advisors.Single(a => a.Role == 0);

        while (game.LastThinker != p.Index)
        {
            game.Step();
        }

        Assert.DoesNotContain(holder, p.Advisors);
        Assert.Equal(0, governor.Role);
        Assert.Equal(Advisor.NoStar, governor.Star);
        Assert.Equal([0.25, 0.0, 0.0, 0.0], governor.Skills);
        Assert.Single(p.Advisors, a => a.IsGovernor && a.Star == star);
    }
}
