using OpenPax.Ui.Forms;
using OpenPax.Ui.Screens;
using static OpenPax.Ui.Tests.UiGames;

namespace OpenPax.Ui.Tests;

/// <summary>An update found is offered on the main menu once; YES asks the host to install it and shows the download (docs/updates.md).</summary>
public sealed class UpdateOfferTests
{
    private static UpdateDialog Dialog(UiApp app)
    {
        var form = app.Forms.Top;
        Assert.NotNull(form);
        return Assert.IsType<UpdateDialog>(form.CodeBehind);
    }

    [Fact]
    public void TheMenuOffersAnUpdateOnceAndUpdateInstallsIt()
    {
        var app = NewApp();
        app.Updates.Running = "0.1.3";
        app.Updates.Available = "0.1.4";
        _ = app.Tick(0).ToList();

        // A true-color dialog, not the game's popup (whose art took the main menu's palette).
        Assert.Null(app.Popup);
        var dialog = Dialog(app);
        Assert.Contains("0.1.4", dialog.Message, StringComparison.Ordinal);
        Assert.Equal(OpenPax.Forms.Layout.Visibility.Visible, dialog.OfferShown);

        // While it is open, the menu under it takes no clicks: Single Player Game stays put.
        var single = app.Current.Window.Get(31100);
        Assert.True(app.FormsMouseDown((single.Left + 5) * 2, (single.Top + 5) * 2, false));
        app.FormsMouseUp((single.Left + 5) * 2, (single.Top + 5) * 2);
        Assert.Equal(OpenPax.Ui.Screens.ScreenMode.GameStart, app.Mode);

        dialog.OnUpdate();
        Assert.Contains(app.Tick(0), r => r.Kind == HostRequestKind.Update);

        // The download shows in the same dialog; CANCEL asks the host to stop.
        Assert.Equal(OpenPax.Forms.Layout.Visibility.Visible, dialog.WorkingShown);
        app.Updates.Busy = true;
        app.Updates.Progress = 0.4;
        app.Updates.Status = "Downloading OpenPax 0.1.4... 40%";
        _ = app.Tick(0).ToList();
        Assert.Equal("Downloading OpenPax 0.1.4... 40%", dialog.Message);
        Assert.Equal(400, dialog.Progress);
        dialog.OnCancel();
        Assert.Contains(app.Tick(0), r => r.Kind == HostRequestKind.CancelUpdate);
    }

    [Fact]
    public void LaterClosesItForTheRunAndAFailedDownloadSaysWhy()
    {
        var app = NewApp();
        app.Updates.Available = "0.1.4";
        _ = app.Tick(0).ToList();
        Dialog(app).OnLater();
        _ = app.Tick(0).ToList();
        Assert.Null(app.Forms.Top); // asked once a run

        // Options can still start it; when the host stops short, the dialog says why.
        app.StartUpdate();
        var dialog = Dialog(app);
        app.Updates.Busy = false;
        app.Updates.Status = "The update was cancelled; nothing changed.";
        _ = app.Tick(0).ToList();
        Assert.Equal("The update was cancelled; nothing changed.", dialog.Message);
        Assert.Equal(OpenPax.Forms.Layout.Visibility.Visible, dialog.DoneShown);
        dialog.OnOk();
        Assert.Null(app.Forms.Top);
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
