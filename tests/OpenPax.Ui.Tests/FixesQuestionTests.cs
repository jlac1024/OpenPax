using OpenPax.Forms;
using OpenPax.Forms.Controls;
using OpenPax.Render;
using OpenPax.Render.TrueColor;
using static OpenPax.Ui.Tests.UiGames;

namespace OpenPax.Ui.Tests;

/// <summary>The one-time question on the main menu: every fix to the original's bugs, or none.</summary>
public sealed class FixesQuestionTests
{
    [Fact]
    public void TurningThemOnTurnsOnEveryFixAndAsksNoMore()
    {
        var app = NewApp();
        app.OfferFixes = true;
        _ = app.Tick(0).ToList();
        var form = Assert.IsType<Form>(app.Forms.Top);

        // OPENPAX_TEST_SHOT=folder: the question saved as a picture, to look at.
        if (Environment.GetEnvironmentVariable("OPENPAX_TEST_SHOT") is { Length: > 0 } folder)
        {
            var canvas = new Canvas(1280, 960);
            app.DrawCanvas(canvas, ClassicPlacement.For(1280, 960, 640, 480));
            using var file = File.Create(Path.Combine(folder, "fixes-question.png"));
            OpenPax.Data.Png.PngWriter.WriteRgba(file, canvas.Width, canvas.Height, System.Runtime.InteropServices.MemoryMarshal.AsBytes(canvas.Pixels.AsSpan()));
        }

        Press(app, form, "TurnOn");
        Assert.All(Preferences.FixNames.Values, name => Assert.True(app.Preferences.Flag(name), name));
        Assert.True(app.Preferences.Flag(Preferences.AskedFixes));
        Assert.Null(app.Forms.Top);

        app.OfferFixes = true;
        _ = app.Tick(16).ToList();
        Assert.Null(app.Forms.Top);
    }

    [Fact]
    public void KeepingTheOriginalChangesNoFix()
    {
        var app = NewApp();
        app.OfferFixes = true;
        _ = app.Tick(0).ToList();
        Press(app, Assert.IsType<Form>(app.Forms.Top), "Original");
        Assert.All(Preferences.FixNames.Values, name => Assert.False(app.Preferences.Flag(name), name));
        Assert.True(app.Preferences.Flag(Preferences.AskedFixes));
    }

    [Fact]
    public void TestsAndScriptedRunsAreNotAsked()
    {
        var app = NewApp();
        _ = app.Tick(0).ToList();
        Assert.Null(app.Forms.Top);
    }

    // A click at the button's middle, in canvas pixels as the host sends it, once the form is laid out.
    private static void Press(UiApp app, Form form, string name)
    {
        var canvas = new Canvas(1280, 960);
        app.DrawCanvas(canvas, ClassicPlacement.For(1280, 960, 640, 480));
        var button = Assert.Single(form.Descendants().OfType<Button>(), b => b.Name == name);
        var (x, y) = ((button.Bounds.X + (button.Bounds.Width / 2)) * app.Forms.Scale, (button.Bounds.Y + (button.Bounds.Height / 2)) * app.Forms.Scale);
        app.FormsMouseMove(x, y);
        Assert.True(app.FormsMouseDown(x, y, false));
        Assert.True(app.FormsMouseUp(x, y));
    }
}
