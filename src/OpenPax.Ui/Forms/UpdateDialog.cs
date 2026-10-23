using System.Globalization;
using OpenPax.Forms;
using OpenPax.Forms.Layout;

namespace OpenPax.Ui.Forms;

/// <summary>
/// An update from GitHub (docs/updates.md, Forms/Markup/Update.xml): the
/// offer, then the download with its progress, then why it stopped short.
/// In true color: the game's own popup art is drawn in each screen's
/// palette, and over the main menu's it came out pink.
/// </summary>
public sealed class UpdateDialog : Observable
{
    private readonly UiApp _app;
    private UpdateStage _stage;

    private UpdateDialog(UiApp app, UpdateStage stage)
    {
        _app = app;
        _stage = stage;
        Form = GameForms.Read("Update", this);
        Form.Bind(this);
        Follow();
    }

    public Form Form { get; }

    public string Message
    {
        get;
        private set => Set(ref field, value);
    } = "";

    /// <summary>How far the download has got, 0 to 1000.</summary>
    public int Progress
    {
        get;
        private set => Set(ref field, value);
    }

    public Visibility OfferShown => _stage == UpdateStage.Offer ? Visibility.Visible : Visibility.Collapsed;

    public Visibility WorkingShown => _stage == UpdateStage.Working ? Visibility.Visible : Visibility.Collapsed;

    public Visibility DoneShown => _stage == UpdateStage.Done ? Visibility.Visible : Visibility.Collapsed;

    /// <summary>Whether it is still on the screen.</summary>
    public bool IsOpen => _app.Forms.Contains(Form);

    /// <summary>The offer of <see cref="UpdateState.Available"/>.</summary>
    public static UpdateDialog Offer(UiApp app) => Show(new UpdateDialog(app, UpdateStage.Offer));

    /// <summary>The download under way (it was started from Options).</summary>
    public static UpdateDialog Working(UiApp app) => Show(new UpdateDialog(app, UpdateStage.Working));

    public void OnUpdate()
    {
        _app.StartUpdate();
        To(UpdateStage.Working);
    }

    public void OnLater() => Close();

    public void OnCancel() => _app.Request(new HostRequest(HostRequestKind.CancelUpdate));

    public void OnOk() => Close();

    /// <summary>Each frame: the text and the bar follow the host's work; a download that stops shows why.</summary>
    public void Follow()
    {
        var u = _app.Updates;
        switch (_stage)
        {
            case UpdateStage.Offer:
                Message = string.Format(CultureInfo.InvariantCulture, UiText.UpdateOffer, u.Available, u.Running);
                break;
            case UpdateStage.Working when u.Busy:
                Message = u.Status;
                Progress = (int)(Math.Clamp(u.Progress, 0, 1) * 1000);
                break;
            case UpdateStage.Working:
                Message = u.Status;
                To(UpdateStage.Done);
                break;
        }
    }

    private void Close() => _app.Forms.Close(Form);

    private void To(UpdateStage stage)
    {
        _stage = stage;
        Changed(nameof(OfferShown));
        Changed(nameof(WorkingShown));
        Changed(nameof(DoneShown));
    }

    private static UpdateDialog Show(UpdateDialog dialog)
    {
        dialog._app.Forms.Show(dialog.Form);
        return dialog;
    }
}
