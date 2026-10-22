using OpenPax.Ui.Widgets;

namespace OpenPax.Ui;

/// <summary>
/// The yes or no popup (kind 0x1A, VIEW 38500): a question, YES and NO.
/// YES (or Return or Tab) does what was asked and closes it; NO closes it.
/// While it is open it takes the mouse and keys, and the screen's keys are
/// refused, but the game clock runs on.
/// </summary>
public sealed class ConfirmPopup : IPopup
{
    public const int ViewId = 38500;
    private const int Yes = 38501;
    private const int No = 38502;
    private const int Question = 38503;

    private readonly Action _onYes;
    private readonly Action? _onNo;

    /// <param name="paletteIndex">The popup takes the palette of the window it opens over.</param>
    public ConfirmPopup(UiAssets assets, int paletteIndex, string question, Action onYes, int screenWidth = 640, int screenHeight = 480, Action? onNo = null)
    {
        assets.Cursor.Show(MouseCursor.Arrow);
        // Open: where the original puts it on screen; the port centers it.
        var size = OpenPax.Data.Views.ViewReader.Parse(assets.Views.Get(OpenPax.Data.Resources.ResourceType.View, ViewId).Span).Base;
        Window = new ViewWindow(assets, ViewId, paletteIndex, ViewId, null, (screenWidth - size.Width) / 2, (screenHeight - size.Height) / 2);
        Window.Get<WrappedTextPane>(Question).Text = question;
        _onYes = onYes;
        _onNo = onNo;
    }

    /// <summary>
    /// The port's own: the same popup as a notice with one button, OK, in
    /// the middle where YES and NO were (a game saved, for one).
    /// </summary>
    public static ConfirmPopup Notice(UiAssets assets, int paletteIndex, string text, int screenWidth = 640, int screenHeight = 480)
    {
        var popup = new ConfirmPopup(assets, paletteIndex, text, () => { }, screenWidth, screenHeight);
        var (yes, no) = (popup.Window.Get<ButtonPane>(Yes), popup.Window.Get<ButtonPane>(No));
        no.Visible = false;
        yes.Caption = UiText.NoticeOk;
        yes.MoveTo((yes.Left + no.Left) / 2, yes.Top);
        return popup;
    }

    public ViewWindow Window { get; }

    /// <summary>Handles what the popup's widgets said; true once it should close.</summary>
    public bool Handle(UiMessage message)
    {
        switch (message.Message)
        {
            case Yes:
                _onYes();
                return true;
            case No:
                _onNo?.Invoke();
                return true;
            default:
                return false;
        }
    }

    /// <summary>Return and Tab answer YES; true once it should close.</summary>
    public bool Key(UiKey key)
    {
        if (key is UiKey.Return or UiKey.Tab)
        {
            _onYes();
            return true;
        }

        return false;
    }
}
