using OpenPax.Ui.Forms;
using OpenPax.Ui.Screens;
using OpenPax.Ui.Widgets;
using static OpenPax.Ui.Tests.UiGames;

namespace OpenPax.Ui.Tests;

/// <summary>An update found is offered on the main menu once; YES asks the host to install it and shows the download (docs/updates.md).</summary>
public sealed class UpdateOfferTests
{
    private const int Yes = 38501;
    private const int No = 38502;

    private static List<HostRequest> Press(UiApp app, int button)
    {
        var pane = app.Popup!.Window.Get(button); // the test opens the popup first
        return Click(app, pane.Left + 5, pane.Top + 5);
    }

    [Fact]
    public void TheMenuOffersAnUpdateOnceAndYesInstallsIt()
    {
        var app = NewApp();
        app.Updates.Running = "0.1.2";
        app.Updates.Available = "0.1.3";
        _ = app.Tick(0).ToList();

        Assert.IsType<ConfirmPopup>(app.Popup);
        Assert.Contains("0.1.3", app.Popup.Window.Get<WrappedTextPane>(38503).Text, StringComparison.Ordinal);
        var asked = Press(app, Yes);
        Assert.Contains(asked, r => r.Kind == HostRequestKind.Update);

        // The download's popup follows the host's work, and CANCEL asks it to stop.
        var downloading = Assert.IsType<UpdatePopup>(app.Popup);
        app.Updates.Busy = true;
        app.Updates.Status = "Downloading OpenPax 0.1.3... 40%";
        _ = app.Tick(0).ToList();
        Assert.Equal("Downloading OpenPax 0.1.3... 40%", downloading.Window.Get<WrappedTextPane>(38503).Text);
        Assert.Contains(Press(app, No), r => r.Kind == HostRequestKind.CancelUpdate);
    }

    [Fact]
    public void NoClosesItForTheRunAndAFailedDownloadSaysWhy()
    {
        var app = NewApp();
        app.Updates.Available = "0.1.3";
        _ = app.Tick(0).ToList();
        _ = Press(app, No);
        Assert.Null(app.Popup);
        _ = app.Tick(0).ToList();
        Assert.Null(app.Popup); // asked once a run

        // Options can still start it; when the host stops short, the popup says why.
        app.StartUpdate();
        Assert.IsType<UpdatePopup>(app.Popup);
        app.Updates.Busy = false;
        app.Updates.Status = "The update was cancelled; nothing changed.";
        _ = app.Tick(0).ToList();
        var notice = Assert.IsType<ConfirmPopup>(app.Popup);
        Assert.Equal("The update was cancelled; nothing changed.", notice.Window.Get<WrappedTextPane>(38503).Text);
    }

    [Fact]
    public void OptionsShowTheVersionAndAskTheHost()
    {
        var app = NewApp();
        app.Updates.Running = "0.1.2";
        var options = ((GameStartScreen)app.Current).OptionsPane;
        Click(app, options.Left + 5, options.Top + 5);
        var model = (OptionsModel)((FormScreen)app.Current).Form.CodeBehind!; // the screen's model is the form's code-behind
        Assert.True(model.CheckUpdates);
        Assert.True(model.UpdatePreReleases);
        Assert.Contains("0.1.2", model.UpdateStatus, StringComparison.Ordinal);
        Assert.False(model.CanUpdate);

        model.OnCheckUpdates();
        Assert.Contains(app.Tick(0), r => r.Kind == HostRequestKind.CheckForUpdates);

        model.UpdatePreReleases = false;
        Assert.False(app.Preferences.Flag(Preferences.UpdatePreReleases));
    }
}
