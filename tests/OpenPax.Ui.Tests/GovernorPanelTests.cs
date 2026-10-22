using OpenPax.Ui.Widgets;
using static OpenPax.Ui.Tests.UiGames;

namespace OpenPax.Ui.Tests;

public class GovernorPanelTests
{
    private static (UiApp App, GameSession Session) AtHomeSystem()
    {
        var app = InGame();
        var session = app.Session!;
        session.SetCurrentStar(session.Local.Colonies[0].Star);
        app.Command(UiCommand.SectorDisplay);
        return (app, session);
    }

    private static void Press(UiApp app, int id)
    {
        var pane = app.Current.Window.Get(id);
        Assert.True(pane.Shown && pane.Enabled, $"{id} cannot be pressed");
        Click(app, pane.Left + 5, pane.Top + 5);
    }

    [Fact]
    public void GovernorShowsTheSystemsGovernorInPlaceOfTheDetails()
    {
        var (app, session) = AtHomeSystem();
        var governor = session.Local.Advisors.Single(a => a.IsGovernor && a.Star == session.CurrentStar);
        Press(app, 13103);
        var window = app.Current.Window;
        Assert.True(window.Get(13800).Shown);
        Assert.False(window.Get(13104).Shown);
        Assert.Equal(((int)(governor.Value(2) * 100.0)).ToString(System.Globalization.CultureInfo.InvariantCulture) + "% ", window.Get<TextLinePane>(13803).Text);
        Assert.Equal(((int)(governor.Value(3) * 100.0)).ToString(System.Globalization.CultureInfo.InvariantCulture) + "% ", window.Get<TextLinePane>(13806).Text);
        Assert.Contains(session.Current.Name, window.Get<WrappedTextPane>(13811).Text);
        _ = app.Tick(10).ToList();
        app.Draw(new OpenPax.Render.Framebuffer(640, 480));

        Press(app, 13103);
        Assert.False(window.Get(13800).Shown);
        Assert.True(window.Get(13104).Shown);
    }

    [Fact]
    public void FireGivesTheSystemANewGovernor()
    {
        var (app, session) = AtHomeSystem();
        var old = session.Local.Advisors.Single(a => a.IsGovernor && a.Star == session.CurrentStar);
        Press(app, 13103);
        Press(app, 13801);
        var now = session.Local.Advisors.Single(a => a.IsGovernor && a.Star == session.CurrentStar);
        Assert.NotEqual(old.Id, now.Id);
        Assert.DoesNotContain(old, session.Local.Advisors);
    }
}
