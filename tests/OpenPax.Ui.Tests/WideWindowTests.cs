using OpenPax.Render;
using OpenPax.Render.TrueColor;
using static OpenPax.Ui.Tests.UiGames;

namespace OpenPax.Ui.Tests;

/// <summary>A window wider than 4:3: the room beside the original screens' picture is filled, the picture itself left to show through.</summary>
public sealed class WideWindowTests
{
    private static byte Alpha(Canvas c, int x, int y) => new Color(c.Pixels[(y * c.Width) + x]).A;

    [Fact]
    public void TheBarsBesideTheOriginalScreensAreFilledAndTheMiddleIsClear()
    {
        var app = NewApp();
        var canvas = new Canvas(1920, 1080);
        var place = ClassicPlacement.For(1920, 1080, 640, 480);
        Assert.True(place.X > 0);
        _ = app.Tick(0).ToList();
        app.DrawCanvas(canvas, place);

        Assert.Equal(255, Alpha(canvas, place.X / 2, 540));
        Assert.Equal(255, Alpha(canvas, 1920 - (place.X / 2), 540));
        Assert.Equal(0, Alpha(canvas, 960, 540));
    }

    [Fact]
    public void AFourByThreeWindowHasNoBars()
    {
        var app = NewApp();
        var canvas = new Canvas(1280, 960);
        var place = ClassicPlacement.For(1280, 960, 640, 480);
        _ = app.Tick(0).ToList();
        app.DrawCanvas(canvas, place);
        Assert.Equal(0, Alpha(canvas, 2, 480));
    }

    [Fact]
    public void TheSectorDisplayWidensAtItsColumnAndBackAgain()
    {
        var app = InGame();
        app.Command(UiCommand.SectorDisplay);
        var w = app.Current.Window;
        app.FitWindow(1920, 1080);
        Assert.Equal((960, 540, 2), (app.ClassicWidth, app.ClassicHeight, app.FrameScale)); // the window's own pixels at 2x
        var extra = 960 - 640;

        Assert.Equal((58, 104), (w.Get(13103).Left, w.Get(13103).Width)); // GOVERNOR, left of the column: stays
        Assert.Equal((452 + extra, 303 + 30), (w.Get(13200).Left, w.Get(13200).Top)); // the planet buttons, right of it and below the first row span: move over and down
        Assert.Equal(452 + extra, w.Get(13201).Left);
        Assert.Equal((178, 441 + extra, 261 + 30), (w.Get(13100).Left, w.Get(13100).Width, w.Get(13100).Height)); // the system view grows both ways
        Assert.Equal((179, 266 + extra, 134 + 30), (w.Get(13101).Left, w.Get(13101).Width, w.Get(13101).Height)); // as does the galaxy map's pane,
        var galaxy = w.Get<Widgets.MapPane>(13101);
        Assert.InRange((galaxy.Width - (2 * galaxy.ContentInset)) * 134 / 266 - (galaxy.Height - (2 * galaxy.ContentInsetY)), -2, 2); // its galaxy keeping its shape, centered
        Assert.Equal(457 + 60, w.Get(13103).Top); // GOVERNOR, below both row spans, moves down

        app.FitWindow(1280, 960);
        Assert.Equal((640, 480), (app.ClassicWidth, app.ClassicHeight));
        Assert.Equal((452, 178, 441, 179, 266), (w.Get(13200).Left, w.Get(13100).Left, w.Get(13100).Width, w.Get(13101).Left, w.Get(13101).Width));
        Assert.Equal(0, w.Get<Widgets.MapPane>(13100).ContentInset);
    }

    [Fact]
    public void AScreenThatDoesNotWidenStaysFourByThree()
    {
        var app = InGame();
        Assert.Equal(Screens.ScreenMode.MainControl, app.Mode);
        app.FitWindow(1920, 1080);
        Assert.Equal(640, app.ClassicWidth);
    }

    [Fact]
    public void TheTopBarsBackButtonMovesWithTheWidth()
    {
        var app = InGame();
        app.Command(UiCommand.SectorDisplay);
        app.FitWindow(1920, 1080);
        _ = app.Tick(0).ToList();
        Click(app, 601 + (app.ClassicWidth - 640) + 10, 8);
        Assert.Equal(Screens.ScreenMode.MainControl, app.Mode);
    }

    [Fact]
    public void ResearchGivesTheRoomToTheTechNames()
    {
        var app = InGame();
        app.Command(UiCommand.Research);
        var w = app.Current.Window;
        app.FitWindow(1920, 1080);
        var extra = app.ClassicWidth - 640;
        Assert.Equal(381 + extra, w.Get(18100).Width); // the list grows
        Assert.Equal(100, w.Get(18002).Left); // tabs left of the gap stay, those right of it move
        Assert.Equal(179 + extra, w.Get(18003).Left);
        Assert.Equal((150 + extra, 127), (w.Get(18071).Left, w.Get(18071).Width)); // the figures keep their size
        Assert.Equal(25 + ((259 + extra) / 2) - 60, w.Get(18200).Left); // the tech's picture sits under the title, mid panel
        Assert.Equal(426 + extra, w.Get(18050).Left); // the projects column moves over
    }

    [Fact]
    public void FleetCommandGivesTheRoomToTheFleetNames()
    {
        var app = InGame();
        app.Command(UiCommand.FleetCommand);
        var w = app.Current.Window;
        app.FitWindow(1920, 1080);
        var extra = app.ClassicWidth - 640;
        Assert.Equal(373 + extra, w.Get(20041).Width); // the fleet list grows
        Assert.Equal(extra / 2, w.Get<Widgets.MapPane>(20001).ContentInset); // the galaxy keeps its shape
        Assert.Equal(270 + extra, w.Get(20002).Left); // the advisor moves over
        Assert.Equal(126 + extra, w.Get(20008).Width); // FLEET LIST stretches with its slot
        Assert.Equal(239 + extra, w.Get(20009).Left); // SHIPYARD LIST moves
    }

    [Fact]
    public void DomesticPolicyGrowsByWholePlanetColumns()
    {
        var app = InGame();
        app.Command(UiCommand.DomesticPolicy);
        var w = app.Current.Window;
        app.FitWindow(1920, 1080);
        Assert.Equal(640 + (3 * 88), app.ClassicWidth); // 320 to spare makes three columns; the rest is bars
        Assert.Equal(440 + (3 * 88), w.Get(17001).Width); // the planet list shows three more
        Assert.Equal(495 + (3 * 88), w.Get(17014).Left); // the advisor moves over
    }

    [Fact]
    public void ForeignAffairsGrowsByWholeEmpireSlots()
    {
        var app = InGame();
        app.Command(UiCommand.ForeignAffairs);
        var w = app.Current.Window;
        app.FitWindow(1920, 1080);
        var extra = app.ClassicWidth - 640;
        Assert.Equal(0, extra % 37); // whole slots
        Assert.Equal(555 + extra, w.Get(15032).Width); // the slot row shows more empires
        Assert.Equal((274 + extra, 66), (w.Get(15024).Left, w.Get(15024).Width)); // the status moves with the other figures
        Assert.Equal(496 + extra, w.Get(15029).Left); // the advisor moves over
    }

    [Fact]
    public void ShipDesignGrowsRightOfItsDesignPanels()
    {
        var app = InGame();
        app.Command(UiCommand.ShipDesign);
        var w = app.Current.Window;
        app.FitWindow(1920, 1080);
        var extra = app.ClassicWidth - 640;
        Assert.Equal(471 + extra, w.Get(26032).Width); // the ship view grows
        Assert.Equal((318 + extra, 193), (w.Get(26039).Left, w.Get(26039).Width)); // the shield diagram keeps its size, moved over
        Assert.Equal(55, w.Get(26210).Left); // the stepped hull buttons stay as they are
        Assert.Equal(528 + extra, w.Get(26011).Left); // the button column moves over
    }

    [Fact]
    public void PlanetarySurfaceGrowsInItsFiguresBlock()
    {
        var app = InGame();
        app.Command(UiCommand.PlanetarySurface);
        var w = app.Current.Window;
        app.FitWindow(1920, 1080);
        var extra = app.ClassicWidth - 640;
        Assert.Equal(0, extra % 15); // whole grid cells
        Assert.Equal(640 + extra, w.Get(14001).Width); // the surface spans the window
        Assert.Equal(64, w.Get(14031).Left); // EXTEND LIST and the orbitals stay
        Assert.Equal((425 + (extra / 2), 197), (w.Get(14014).Left, w.Get(14014).Width)); // the figures keep their size, centered in the block
        Assert.Equal(597 + extra, w.Get(14003).Left); // the next planet arrow moves to the block's end
    }

    [Fact]
    public void GameOptionsGrowsLeftOfItsSpinners()
    {
        var app = InGame();
        app.Command(UiCommand.GameOptions);
        var w = app.Current.Window;
        app.FitWindow(1920, 1080);
        var extra = app.ClassicWidth - 640;
        Assert.Equal(0, extra % 15); // whole grid cells
        Assert.Equal(519 + extra, w.Get(32005).Left); // QUIT GAME moves over
        Assert.Equal((464 + extra, 101), (w.Get(32057).Left, w.Get(32057).Width)); // a spinner keeps its size, moved over
        Assert.Equal(354, w.Get(32004).Left); // NEW GAME stays
    }

    [Fact]
    public void CombatShowsMoreOfTheFieldAcross()
    {
        var app = InGame();
        app.Command(UiCommand.Mode20);
        var w = app.Current.Window;
        app.FitWindow(1920, 1080);
        var extra = app.ClassicWidth - 640;
        Assert.Equal(0, extra % 52); // whole slots of the controls panel
        Assert.Equal(600 + extra, w.Get(24001).Width); // the battlefield grows
        Assert.Equal(600 + extra, w.Get(24002).Width); // as does the controls panel's art,
        Assert.Equal((191 + extra, 56), (w.Get(24100).Left, w.Get(24100).Width)); // its buttons keeping their size, moved over
        Assert.Equal((186 + extra, 20), (w.Get(24501).Left, w.Get(24501).Width)); // the indicators keep their size, moved over
    }

    [Fact]
    public void TheWidenedSystemViewIsFilledWithItsNebula()
    {
        var app = InGame();
        app.Command(UiCommand.SectorDisplay);
        app.FitWindow(1920, 1080);
        _ = app.Tick(0).ToList();
        var frame = new OpenPax.Render.Framebuffer(app.ClassicWidth, app.ClassicHeight);
        app.Draw(frame);
        var view = app.Current.Window.Get(13100);

        // Some starlight near the view's right end, where the nebula alone once stopped short.
        var lit = 0;
        for (var y = view.Top + 40; y < view.Top + view.Height - 40; y++)
        {
            for (var x = view.Left + view.Width - 60; x < view.Left + view.Width - 10; x++)
            {
                lit += frame.Pixels[(y * frame.PixelWidth) + x] != 0 ? 1 : 0;
            }
        }

        Assert.True(lit > 20, $"{lit} lit pixels");
    }
}
