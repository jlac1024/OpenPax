using OpenPax.Sim.Galaxy;
using OpenPax.Ui.Screens;
using OpenPax.Ui.Widgets;
using static OpenPax.Ui.Tests.UiGames;

namespace OpenPax.Ui.Tests;

public class SaveLoadTests
{
    private static (UiApp App, MemorySaves Saves) WithSaves()
    {
        var app = InGame();
        var saves = new MemorySaves();
        app.SaveGames = saves;
        return (app, saves);
    }

    private static void Press(UiApp app, int id)
    {
        var window = app.Popup?.Window ?? app.Current.Window;
        var pane = window.Get(id);
        Assert.True(pane.Shown && pane.Enabled, $"{id} cannot be pressed");
        Click(app, pane.Left + 5, pane.Top + 5);
    }

    [Fact]
    public void ASaveKeepsWhetherCheatsWereOn()
    {
        var (app, saves) = WithSaves();
        app.Console.CheatsOn = true;
        app.Command(UiCommand.QuickSave);
        Assert.Contains(UiApp.QuickSaveName, saves.Files.Keys);
        app.Console.CheatsOn = false;

        Assert.True(app.LoadGame(UiApp.QuickSaveName, 0));
        Assert.True(app.Console.CheatsOn);
    }

    [Fact]
    public void ASaveIsMadeFromTheSavePanelAndLoadedFromTheLoadPanel()
    {
        var (app, saves) = WithSaves();
        var session = app.Session!;
        for (var i = 0; i < 300; i++)
        {
            session.Game.Step();
        }

        var planet = session.Local.Colonies[0].Planet;
        session.Select(planet);
        session.ResearchField = 3;
        var hash = session.Game.Hash();

        app.Command(UiCommand.GameOptions);
        Press(app, 32002);
        var edit = app.Current.Window.Get<TextEditPane>(32014);
        Click(app, edit.Left + 5, edit.Top + 5);
        app.Typed("Test one", 0);
        app.Tick(0).ToList();
        Press(app, 32047);
        Assert.Contains("Test one", saves.Files.Keys);
        Assert.Single(app.Current.Window.Get<ListBoxPane>(32048).Rows);
        Assert.NotNull(app.Popup); // saved: a notice with OK
        Assert.False(app.Popup.Window.Get(38502).Visible);
        Press(app, 38501);
        Assert.Null(app.Popup);

        // The game goes on, then the save is loaded back.
        for (var i = 0; i < 100; i++)
        {
            session.Game.Step();
        }

        Press(app, 32001);
        Press(app, 32042);
        Assert.Equal(ScreenMode.MainControl, app.Mode);
        var loaded = app.Session!;
        Assert.NotSame(session, loaded);
        Assert.Equal(hash, loaded.Game.Hash());
        Assert.Equal(3, loaded.ResearchField);
        var selected = Assert.IsType<Planet>(loaded.Selection);
        Assert.Equal(planet.ObjectId, selected.ObjectId);
        Assert.Same(loaded.Local.Colonies[0].Planet, selected);
        const string notice = "Loaded game Test one successfully.";
        Assert.True(app.Log.History.Any(n => n?.Text == notice) || app.Log.Waiting.Any(n => n.Text == notice), "no load notice");
    }

    [Fact]
    public void QuickSaveAndQuickLoad()
    {
        var (app, saves) = WithSaves();
        var hash = app.Session!.Game.Hash();
        app.Command(UiCommand.QuickSave);
        Assert.Contains(UiApp.QuickSaveName, saves.Files.Keys);

        app.Session.Game.Step();
        app.Command(UiCommand.QuickLoad);
        Assert.Equal(hash, app.Session!.Game.Hash());
    }

    [Fact]
    public void TheAutosaveFollowsThePreference()
    {
        var (app, saves) = WithSaves();
        app.GameEvents(OpenPax.Sim.StepEvents.Autosave);
        Assert.Contains(UiApp.AutoSaveName, saves.Files.Keys);

        saves.Files.Clear();
        app.Preferences.Set(Preferences.AutoSaveGame, 0);
        app.GameEvents(OpenPax.Sim.StepEvents.Autosave);
        Assert.Empty(saves.Files);
    }

    [Fact]
    public void DeleteAsksThenRemovesTheSave()
    {
        var (app, saves) = WithSaves();
        app.SaveGame("Old");
        app.Command(UiCommand.GameOptions);
        Press(app, 32001);
        Press(app, 32043);
        var question = app.Popup!.Window.Get<WrappedTextPane>(38503).Text;
        Assert.StartsWith("Are you sure you want to delete", question);
        Assert.EndsWith("Old?", question);
        Press(app, 38501);
        Assert.Empty(saves.Files);
        Assert.Empty(app.Current.Window.Get<ListBoxPane>(32044).Rows);
    }

    [Fact]
    public void TheStartScreenLoadsASave()
    {
        var (app, saves) = WithSaves();
        app.SaveGame("From start");
        app.NewGame();
        Assert.Null(app.Session);

        Click(app, 400, 110);
        Press(app, 31106);
        var list = app.Current.Window.Get<ListBoxPane>(31111);
        var row = Assert.Single(list.Rows);
        Click(app, list.Left + 10, list.Top + 5);
        Press(app, 31109);
        Assert.Equal(ScreenMode.MainControl, app.Mode);
        Assert.NotNull(app.Session);
    }
}
