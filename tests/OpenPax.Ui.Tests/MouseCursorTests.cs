using OpenPax.Render;
using OpenPax.Ui.Widgets;
using static OpenPax.Ui.Tests.UiGames;

namespace OpenPax.Ui.Tests;

public class MouseCursorTests
{
    private static void HoverAt(UiApp app, int x, int y)
    {
        app.MouseMove(x, y, 0);
        _ = app.Tick(0).ToList();
    }

    [Fact]
    public void HotSpotsSitUnderTheMouse()
    {
        Assert.Equal((7, 2), MouseCursor.HotSpot(MouseCursor.Arrow));
        Assert.Equal((14, 14), MouseCursor.HotSpot(MouseCursor.Target));
        Assert.Equal((15, 15), MouseCursor.HotSpot(MouseCursor.CannotAttack));
    }

    [Fact]
    public void ScreensPickTheirPointer()
    {
        var app = InGame();
        Assert.Equal(MouseCursor.Bridge, app.Assets.Cursor.Set);
        app.Command(UiCommand.Research);
        Assert.Equal(MouseCursor.Arrow, app.Assets.Cursor.Set);
    }

    [Fact]
    public void TheMapPointerFollowsTheOrderWaiting()
    {
        var app = InGame();
        app.Command(UiCommand.SectorDisplay);
        var map = app.Current.Window.Get<MapPane>(13100);
        map.OrderMode = 1;
        Assert.Equal(MouseCursor.Target, app.Assets.Cursor.Set);
        map.OrderMode = 2;
        Assert.Equal(MouseCursor.CannotAttack, app.Assets.Cursor.Set);
        map.OrderMode = 3;
        Assert.Equal(MouseCursor.CannotColonize, app.Assets.Cursor.Set);
        map.OrderMode = 0;
        Assert.Equal(MouseCursor.Arrow, app.Assets.Cursor.Set);
    }

    [Fact]
    public void ASpinnersBoxShowsTheSlidePointer()
    {
        var app = InGame();
        app.Command(UiCommand.DomesticPolicy);
        var spinner = app.Current.Window.Get<SpinnerPane>(17016);
        var seen = new HashSet<int>();
        for (var x = spinner.Left + 1; x < spinner.Left + spinner.Width - 1; x++)
        {
            HoverAt(app, x, spinner.Top + spinner.Height / 2);
            seen.Add(app.Assets.Cursor.Set);
        }

        Assert.Equal([MouseCursor.Arrow, MouseCursor.Slide], seen.Order());
        HoverAt(app, 0, 0);
        Assert.Equal(MouseCursor.Arrow, app.Assets.Cursor.Set);
    }

    [Fact]
    public void AHotSpotShowsItsOwnPointerAndGivesTheOldOneBack()
    {
        // Species Selection's portraits are the only hot spots with a pointer of their own; the first is the one picked, and disabled.
        var app = NewApp();
        Click(app, 400, 110);
        Click(app, 400, 110);
        Assert.Equal(Screens.ScreenMode.SpeciesSelection, app.Mode);
        Assert.Equal(MouseCursor.SpeciesSelection, app.Assets.Cursor.Set);
        var spot = app.Current.Window.Get<HotSpotPane>(11011);
        HoverAt(app, spot.Left + spot.Width / 2, spot.Top + spot.Height / 2);
        Assert.Equal(250, app.Assets.Cursor.Set);
        HoverAt(app, 2, 2);
        Assert.Equal(MouseCursor.SpeciesSelection, app.Assets.Cursor.Set);
    }

    [Fact]
    public void ThePointerIsDrawnOnlyWhileTheMouseIsOverTheWindow()
    {
        var app = InGame();
        HoverAt(app, 300, 200);
        var with = new Framebuffer(640, 480);
        app.Draw(with);
        app.Pointer = null;
        var without = new Framebuffer(640, 480);
        app.Draw(without);
        Assert.NotEqual(without.Pixels.ToArray(), with.Pixels.ToArray());
    }
}
