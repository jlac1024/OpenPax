using OpenPax.Forms;
using OpenPax.Forms.Controls;

namespace OpenPax.Ui.Forms;

/// <summary>The chat line of a network game (Forms/Markup/Chat.xml): typed, sent on Return, closed on Escape.</summary>
public sealed class ChatBar : Observable
{
    private readonly UiApp _app;

    private ChatBar(UiApp app)
    {
        _app = app;
        Form = GameForms.Read("Chat", this);
        Form.Bind(this);
    }

    public Form Form { get; }

    public string Line
    {
        get;
        set => Set(ref field, value);
    } = "";

    public static ChatBar Open(UiApp app)
    {
        var bar = new ChatBar(app);
        app.Forms.Show(bar.Form);
        bar.Form.Focus(bar.Form.Get<TextBox>("Line"));
        return bar;
    }

    public void OnSend()
    {
        if (Line.Trim().Length > 0)
        {
            _app.Session?.Link?.Say(Line);
        }

        Close();
    }

    public void Close() => _app.Forms.Close(Form);
}
