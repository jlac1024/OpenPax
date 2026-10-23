using OpenPax.Ui.Widgets;

namespace OpenPax.Ui;

/// <summary>
/// The port's own: an update downloading (docs/updates.md), in the game's
/// yes or no popup (VIEW 38500) with only CANCEL, its text following the
/// download. The game restarts on its own when the update is in place.
/// </summary>
public sealed class UpdatePopup : IPopup
{
    private const int Yes = 38501;
    private const int No = 38502;
    private const int Question = 38503;

    private readonly Action _onCancel;
    private string _shown = "";

    public UpdatePopup(UiAssets assets, int paletteIndex, Action onCancel, int screenWidth = 640, int screenHeight = 480)
    {
        assets.Cursor.Show(MouseCursor.Arrow);
        var size = OpenPax.Data.Views.ViewReader.Parse(assets.Views.Get(OpenPax.Data.Resources.ResourceType.View, ConfirmPopup.ViewId).Span).Base;
        Window = new ViewWindow(assets, ConfirmPopup.ViewId, paletteIndex, ConfirmPopup.ViewId, null, (screenWidth - size.Width) / 2, (screenHeight - size.Height) / 2);
        var (yes, no) = (Window.Get<ButtonPane>(Yes), Window.Get<ButtonPane>(No));
        yes.Visible = false;
        no.Caption = UiText.UpdateCancel;
        no.MoveTo((yes.Left + no.Left) / 2, no.Top);
        _onCancel = onCancel;
    }

    public ViewWindow Window { get; }

    /// <summary>Shows where the download stands.</summary>
    public void Show(UpdateState state)
    {
        var text = state.Status;
        if (text != _shown)
        {
            _shown = text;
            Window.Get<WrappedTextPane>(Question).Text = text;
        }
    }

    public bool Handle(UiMessage message)
    {
        if (message.Message == No)
        {
            _onCancel();
            return true;
        }

        return false;
    }

    /// <summary>Escape cancels; nothing else does anything.</summary>
    public bool Key(UiKey key)
    {
        if (key == UiKey.Escape)
        {
            _onCancel();
            return true;
        }

        return false;
    }
}
