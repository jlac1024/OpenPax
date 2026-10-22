using OpenPax.Data.Views;
using OpenPax.Forms;

namespace OpenPax.Ui.Screens;

/// <summary>
/// A screen made as a form (docs/ui/forms.md): the original screens'
/// picture behind it stays black and the form fills the window in true
/// color. The form shows while the screen does.
/// </summary>
public abstract class FormScreen : Screen
{
    private static readonly ViewNode _blank = new(8000, new WidgetBase(0, 0, 0, 640, 480, 2, 2), null, [], "", null);

    protected FormScreen(UiApp app, Form form)
        : base(app, new ViewWindow(app.Assets, _blank, Forms.GameTheme.Palette, 0))
    {
        Form = form;
        app.Assets.Cursor.Show(MouseCursor.Arrow);
        app.Forms.Show(form);
    }

    public Form Form { get; }

    public override void Handle(UiMessage message, long now)
    {
    }

    public override void Closed() => App.Forms.Close(Form);
}
