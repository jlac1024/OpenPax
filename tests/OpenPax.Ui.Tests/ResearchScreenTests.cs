using OpenPax.Sim;
using OpenPax.Sim.Players;
using OpenPax.Ui.Screens;
using OpenPax.Ui.Widgets;
using static OpenPax.Ui.Tests.UiGames;

namespace OpenPax.Ui.Tests;

public class ResearchScreenTests
{
    private static (UiApp App, GameSession Session) AtResearch()
    {
        var app = InGame();
        app.Command(UiCommand.Research);
        Assert.Equal(ScreenMode.Research, app.Mode);
        return (app, app.Session!);
    }

    private static void Press(UiApp app, int id)
    {
        var pane = app.Current.Window.Get(id);
        Click(app, pane.Left + 5, pane.Top + 5);
    }

    [Fact]
    public void TheListHoldsTheFieldsResearchableTechs()
    {
        var (app, session) = AtResearch();
        var list = app.Current.Window.Get<ListBoxPane>(18100);
        var expected = session.Game.Tables.Techs.Count(t => t.ResearchField == 0 && Game.Researchable(session.Local, t));
        Assert.Equal(expected, list.Rows.Count);
        Assert.All(list.Rows.Cast<TechRow>(), r => Assert.Equal(0, r.Tech.ResearchField));

        Press(app, 18003);
        Assert.All(list.Rows.Cast<TechRow>(), r => Assert.Equal(2, r.Tech.ResearchField));
        Assert.Equal(2, session.ResearchField);
    }

    [Fact]
    public void DevelopedListsWhatIsKnown()
    {
        var (app, session) = AtResearch();
        Press(app, 18009);
        var list = app.Current.Window.Get<ListBoxPane>(18100);
        Assert.All(list.Rows.Cast<TechRow>(), r => Assert.True(session.Local.Knows(r.Tech.Id)));
        Assert.True(session.ResearchShowsKnown);
    }

    [Fact]
    public void ResearchMakesThePickedTechTheFieldsProjectAndKeepsTheOldProgress()
    {
        var (app, session) = AtResearch();
        var player = session.Local;
        var slot = player.ResearchSlots[0];
        var old = slot.Tech;
        slot.Progress = 7;

        var list = app.Current.Window.Get<ListBoxPane>(18100);
        var row = list.Rows.Cast<TechRow>().FirstOrDefault(r => r.Tech.Id != old);
        Assert.SkipWhen(row is null, "Only one weapon tech to research with this seed.");

        // A row is never null past the skip above.
        list.Select(row!);
        Press(app, 18006);

        Assert.Equal(row!.Tech.Id, slot.Tech);
        Assert.Equal(7, player.SavedProgress[old]);
    }

    [Fact]
    public void AnAllocationCannotPassOneHundred()
    {
        var (app, session) = AtResearch();
        var spinner = app.Current.Window.Get<SpinnerPane>(18020);
        Assert.Equal(20, spinner.Value);

        // Quirk kept: right after opening the spinner may go past what is
        // left, but the game holds the field to it.
        var increase = spinner.Left + 17 + 45 + 8;
        Click(app, increase, spinner.Top + 6);
        Assert.Equal(21, spinner.Value);
        Assert.Equal(20, session.Local.Research[0]);
    }

    [Fact]
    public void ClickingAProjectShowsItsField()
    {
        var (app, session) = AtResearch();
        var slot = app.Current.Window.Get<ResearchSlotPane>(18052);
        Click(app, slot.Left + 10, slot.Top + 10);

        Assert.Equal(2, session.ResearchField);
        var picked = (TechRow?)app.Current.Window.Get<ListBoxPane>(18100).Selected;
        Assert.Equal(session.Local.ResearchSlots[2].Tech, picked?.Tech.Id);
        Assert.True(slot.Highlighted);
    }

    [Fact]
    public void TheBridgeStationOpensResearch()
    {
        var app = InGame();
        Press(app, 22002);
        Assert.Equal(ScreenMode.Research, app.Mode);
    }

    [Fact]
    public void TheStatsFollowTheShownTech()
    {
        var (app, _) = AtResearch();
        var list = app.Current.Window.Get<ListBoxPane>(18100);
        var shown = ((TechRow)list.Selected!).Tech.Id;
        Assert.Equal(shown, app.Current.Window.Get<TechStatsPane>(18071).TechId);

        // Every class of tech draws without trouble.
        var frame = new OpenPax.Render.Framebuffer(640, 480);
        for (var field = 0; field < 5; field++)
        {
            Press(app, 18001 + field);
            app.Draw(frame);
        }
    }

    [Fact]
    public void ReplacePromotesAGovernorToResearchAdvisor()
    {
        var (app, session) = AtResearch();
        Press(app, 18007);
        var popup = Assert.IsType<GovernorPopup>(app.Popup);
        var list = popup.Window.Get<ListBoxPane>(39003);
        Assert.Equal(session.Local.Advisors.Count(a => a.IsGovernor), list.Rows.Count);
        Assert.StartsWith("Research Advisor ", popup.Window.Get<TextLinePane>(39005).Text);

        var governor = ((GovernorRow)list.Selected!).Governor;
        var promote = popup.Window.Get(39001);
        Click(app, promote.Left + 5, promote.Top + 5);

        Assert.Equal(0, governor.Role);
        Assert.Same(popup, app.Popup);
        Assert.DoesNotContain(list.Rows.Cast<GovernorRow>(), r => r.Governor == governor);

        var done = popup.Window.Get(39002);
        Click(app, done.Left + 5, done.Top + 5);
        Assert.Null(app.Popup);
    }
}
