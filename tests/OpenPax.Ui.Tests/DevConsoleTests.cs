using OpenPax.Ui.Screens;
using static OpenPax.Ui.Tests.UiGames;

namespace OpenPax.Ui.Tests;

public class DevConsoleTests
{
    private static void Type(UiApp app, string line)
    {
        app.Typed(line, 0);
        app.Key(UiKey.Return, false, 0);
    }

    [Fact]
    public void BacktickOpensTheConsoleAndCheatsNeedTurningOn()
    {
        var app = InGame();
        var local = app.Session!.Local;
        var money = local.Money;
        app.Command(UiCommand.Console);
        Assert.True(app.Console.Open);

        // The opening key's own character is not typed.
        app.Typed("`", 0);
        Assert.Equal("", app.Console.Input);

        Type(app, "money");
        Assert.Equal(money, local.Money);
        Assert.Contains("Cheats are off", app.Console.Output[^1], StringComparison.Ordinal);

        Type(app, "cheats on");
        Type(app, "money");
        Assert.Equal(900000000.0, local.Money);

        // Escape closes the console and nothing else.
        var mode = app.Mode;
        app.Command(UiCommand.GameOptions);
        Assert.False(app.Console.Open);
        Assert.Equal(mode, app.Mode);
        app.Draw(new OpenPax.Render.Framebuffer(640, 480));
    }

    [Fact]
    public void TheCheatWindowGivesTheCheatToThePlayerPicked()
    {
        var app = InGame();
        app.Command(UiCommand.Console);
        Type(app, "cheats on");
        Type(app, "devwindow");
        Assert.Equal(ScreenMode.Developer, app.Mode);
        Assert.False(app.Console.Open);
        var button = app.Current.Window.Get(27003);
        Click(app, button.Left + 5, button.Top + 5);
        Assert.Equal(900000000.0, app.Session!.Game.Players[0].Money);
        app.Draw(new OpenPax.Render.Framebuffer(640, 480));
    }

    [Fact]
    public void ACheatForAnotherPlayerTakesItsNumber()
    {
        var app = InGame();
        app.Command(UiCommand.Console);
        Type(app, "cheats on");
        Type(app, "kill 2");
        Assert.True(app.Session!.Game.Players[2].Eliminated);
        Type(app, "kill 99");
        Assert.Contains("No such player", app.Console.Output[^1], StringComparison.Ordinal);
    }
}
