using OpenPax.Data.Tables;
using OpenPax.Ui.Screens;
using OpenPax.Ui.Widgets;
using static OpenPax.Ui.Tests.UiGames;

namespace OpenPax.Ui.Tests;

public class PlayerDesignTests
{
    /// <summary>At Species Selection with a store for designed species; then Design opens the designer on the first stock species.</summary>
    private static (UiApp App, MemorySpecies Store) AtDesigner()
    {
        var app = NewApp();
        var store = new MemorySpecies();
        app.CustomSpeciesStore = store;
        Click(app, 400, 110);
        Click(app, 400, 110);
        Assert.Equal(ScreenMode.SpeciesSelection, app.Mode);
        Press(app, 11105);
        Assert.Equal(ScreenMode.PlayerDesign, app.Mode);
        return (app, store);
    }

    private static void Press(UiApp app, int id)
    {
        var pane = app.Current.Window.Get(id);
        Assert.True(pane.Shown && pane.Enabled, $"{id} cannot be pressed");
        Click(app, pane.Left + 3, pane.Top + 3);
    }

    private static PlayerDesignScreen Designer(UiApp app) => (PlayerDesignScreen)app.Current;

    private static string Text(UiApp app, int id) => app.Current.Window.Get<TextLinePane>(id).Text;

    [Fact]
    public void TheDesignerStartsFromTheStockSpeciesUnnamed()
    {
        var (app, _) = AtDesigner();
        var species = Designer(app).Species;
        var stock = app.StockSpecies[0];
        Assert.Equal("Unnamed", species.Name);
        Assert.Equal(stock.Description, species.Description);
        Assert.Equal(stock.Points(), species.Points());
        Assert.Equal(stock.Points().ToString(System.Globalization.CultureInfo.InvariantCulture), Text(app, 11343));
        Assert.True(app.Current.Window.Get(11303).Enabled);
        _ = app.Tick(10).ToList();
        app.Draw(new OpenPax.Render.Framebuffer(640, 480));
    }

    [Fact]
    public void ClearGivesTheDefaultsAndTraitsCostPoints()
    {
        var (app, _) = AtDesigner();
        Press(app, 11302);
        var species = Designer(app).Species;
        Assert.Equal(0, species.Points());
        Assert.Equal([true, false, false, false], species.Atmospheres);
        Assert.Equal("10", Text(app, 11344));

        // Subterranean costs 5; two cultural traits 2 each; an extra atmosphere 1.
        Press(app, 11311);
        Press(app, 11315);
        Press(app, 11316);
        Press(app, 11308);
        Assert.Equal(10, Designer(app).Species.Points());
        Assert.True(app.Current.Window.Get(11303).Enabled);
        Press(app, 11317);
        Assert.Equal("-2", Text(app, 11344));
        Assert.False(app.Current.Window.Get(11303).Enabled);
    }

    [Fact]
    public void UntickingTheLastAtmosphereTicksItsPartner()
    {
        var (app, _) = AtDesigner();
        Press(app, 11302);
        Press(app, 11306);
        Assert.Equal([false, true, false, false], Designer(app).Species.Atmospheres);
    }

    [Fact]
    public void AnaerobicHidesTheAtmospheresAndSubterraneanTheTemperatures()
    {
        var (app, _) = AtDesigner();
        Press(app, 11302);
        var marker = app.Current.Window.Get(11352);
        Assert.Equal(536, marker.Left);
        Press(app, 11337);
        Assert.Equal(10, Designer(app).Species.PreferredTemperature);
        Assert.Equal(446, marker.Left);

        Press(app, 11310);
        Assert.False(app.Current.Window.Get(11306).Shown);
        Assert.False(app.Current.Window.Get(11434).Shown);
        Press(app, 11311);
        Assert.False(app.Current.Window.Get(11332).Shown);
        Assert.False(marker.Shown);
    }

    [Fact]
    public void ThePortraitsWrapOverEighteen()
    {
        var (app, _) = AtDesigner();
        Press(app, 11302);
        Press(app, 11304);
        Assert.Equal(17, Designer(app).Species.Picture);
        Assert.Equal(18000, app.Current.Window.Get<PicturePane>(11351).Picture);
        Press(app, 11305);
        Assert.Equal(0, Designer(app).Species.Picture);
    }

    [Fact]
    public void AcceptKeepsTheSpeciesAndGoesToSetupAndSelectionCanShowAndDeleteIt()
    {
        var (app, store) = AtDesigner();
        var designed = Designer(app).Species;
        Press(app, 11303);
        Assert.Equal(ScreenMode.GameSetup, app.Mode);
        Assert.Equal((designed.Name, designed.Description, designed.Points()), (app.ChosenSpecies!.Name, app.ChosenSpecies.Description, app.ChosenSpecies.Points())); // Accept chose it
        Assert.Single(store.Kept);
        Assert.Equal(designed.Name, store.Kept[0].Name);

        // Custom is pressed while the stock figure is still stepping out; its text must not win.
        app.Switch(ScreenMode.SpeciesSelection);
        _ = app.Tick(100).ToList();
        Press(app, 11803);
        var selection = (SpeciesSelectionScreen)app.Current;
        Assert.True(selection.ShowsCustom);
        for (var t = 0; t < 2000 && app.Current.Window.Get<TextLinePane>(11100).Text.Length == 0; t += 50)
        {
            _ = app.Tick(t).ToList();
        }

        for (var t = 2000; t < 4000; t += 50)
        {
            _ = app.Tick(t).ToList();
        }

        Assert.Equal(designed.Name, app.Current.Window.Get<TextLinePane>(11100).Text);
        Press(app, 11104);
        Assert.Empty(store.Kept);
        Assert.False(selection.ShowsCustom);
        Assert.False(app.Current.Window.Get(11803).Enabled);
    }

    [Fact]
    public void CancelKeepsNothing()
    {
        var (app, store) = AtDesigner();
        Press(app, 11301);
        Assert.Equal(ScreenMode.SpeciesSelection, app.Mode);
        Assert.Empty(store.Kept);
        Assert.Equal(0, store.Saves);
    }
}
