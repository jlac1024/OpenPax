using OpenPax.Ui.Widgets;
using static OpenPax.Ui.Tests.UiGames;

namespace OpenPax.Ui.Tests;

public class ComparePopupTests
{
    private static (UiApp App, ComparePopup Popup) Open(int ticks = 0)
    {
        var app = InGame();
        var game = app.Session!.Game;
        for (var i = 0; i < ticks; i++)
        {
            game.Step();
        }

        app.Command(UiCommand.ForeignAffairs);
        var compare = app.Current.Window.Get(15003);
        Click(app, compare.Left + 5, compare.Top + 5);
        return (app, Assert.IsType<ComparePopup>(app.Popup));
    }

    private static void Press(UiApp app, int id)
    {
        var pane = app.Popup!.Window.Get(id);
        Click(app, pane.Left + 5, pane.Top + 5);
    }

    [Fact]
    public void EveryPlayerHasAToggleAndALine()
    {
        var (app, popup) = Open();
        var players = app.Session!.Game.Players;
        var graph = popup.Window.Get<HistoryGraphPane>(42020);
        for (var t = 0; t < 16; t++)
        {
            var toggle = popup.Window.Get<EmpireTogglePane>(42004 + t);
            Assert.Equal(t < players.Count, toggle.Enabled);
            Assert.Equal(t < players.Count ? players[t] : null, toggle.Player);
            Assert.Equal(t < players.Count, graph.LineShown(t));
        }

        Assert.Equal(1, graph.Statistic);
        Assert.Equal("IMPERIAL TREASURY", popup.Window.Get<LabelPane>(42021).Text ?? popup.Window.Get<TextLinePane>(42021).Text);
    }

    [Fact]
    public void ATogglesClickHidesItsLine()
    {
        var (app, popup) = Open();
        Press(app, 42005);
        Assert.False(popup.Window.Get<HistoryGraphPane>(42020).LineShown(1));
        Assert.False(popup.Window.Get<EmpireTogglePane>(42005).On);
    }

    [Fact]
    public void TheArrowsStepAndWrapTheStatistic()
    {
        var (app, popup) = Open();
        var graph = popup.Window.Get<HistoryGraphPane>(42020);
        Press(app, 42002);
        Assert.Equal(10, graph.Statistic);
        Press(app, 42003);
        Press(app, 42003);
        Assert.Equal(2, graph.Statistic);
    }

    [Fact]
    public void TheGraphScalesToTheHighestShownSample()
    {
        var (app, popup) = Open(2000);
        var graph = popup.Window.Get<HistoryGraphPane>(42020);
        var scaled = graph.Scaled();
        var highest = scaled.Where(l => l is not null).SelectMany(l => l!).Max();
        Assert.Equal(graph.Height, highest);
        Assert.Contains(scaled[0]!, v => v >= 0);

        app.Draw(new OpenPax.Render.Framebuffer(640, 480));
        Press(app, 42001);
        Assert.Null(app.Popup);
    }
}
