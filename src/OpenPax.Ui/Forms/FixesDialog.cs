using OpenPax.Forms;

namespace OpenPax.Ui.Forms;

/// <summary>
/// Asked once (Forms/Markup/Fixes.xml): turn on every fix to the original's
/// bugs for new games, or keep the original's rules. Either answer is kept,
/// and each fix stays its own switch in Options, GAME.
/// </summary>
public sealed class FixesDialog : Observable
{
    private readonly UiApp _app;

    private FixesDialog(UiApp app)
    {
        _app = app;
        Form = GameForms.Read("Fixes", this);
        Form.Bind(this);
    }

    public Form Form { get; }

    public string Message => UiText.FixesQuestion;

    public string Note => UiText.FixesNote;

    public static FixesDialog Show(UiApp app)
    {
        var dialog = new FixesDialog(app);
        app.Forms.Show(dialog.Form);
        return dialog;
    }

    public void OnTurnOn()
    {
        foreach (var name in Preferences.FixNames.Values)
        {
            _app.Preferences.Set(name, 1);
        }

        Answered();
    }

    public void OnKeep() => Answered();

    private void Answered()
    {
        _app.Preferences.Set(Preferences.AskedFixes, 1);
        _app.Forms.Close(Form);
    }
}
