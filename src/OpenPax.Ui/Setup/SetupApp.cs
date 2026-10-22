using OpenPax.Forms;
using OpenPax.Forms.Drawing;
using OpenPax.Forms.Input;
using OpenPax.Render;
using OpenPax.Render.TrueColor;

namespace OpenPax.Ui.Setup;

/// <summary>
/// The first-run setup as an app of its own (docs/first-run.md): the setup
/// form on a plain theme, the modern font from beside the program, and no
/// game art, since none is imported yet. The host runs it before the game.
/// </summary>
public sealed class SetupApp
{
    // The forms' layout is the original screens' 640 wide; the window scales it.
    private const int LayoutWidth = 640;

    private readonly FormHost _forms;

    public SetupApp(OpenPax.Render.Fonts.ModernFont? font, SetupModel model)
    {
        Model = model;
        var settings = new TextSettings { Modern = font, Mode = TextMode.Modern };
        var text = BitmapText.ModernOnly(settings, 7);
        var heading = BitmapText.ModernOnly(settings, 11);
        _forms = new FormHost(new Theme
        {
            Fonts = i => i == HeadingFont ? heading : text,
            Art = new NoArt(),
            Panel = Color.Rgb(18, 21, 32),
            PanelFrame = Color.Rgb(128, 136, 156),
            Heading = Color.Rgb(230, 190, 60),
            Well = Color.Rgb(6, 8, 14),
            ButtonText = Color.Rgb(25, 28, 40),
        });
        var form = OpenPax.Ui.Forms.GameForms.Read("Setup", model);
        form.Bind(model);
        _forms.Show(form);
    }

    // The heading's font index: TextBlocks with Look="Heading" ask for it through the theme.
    private const int HeadingFont = 4;

    public SetupModel Model { get; }

    /// <summary>Whether the form has the keyboard (a text box being typed in).</summary>
    public bool HasFocus => _forms.Top?.Focused is not null;

    public void Tick(long now)
    {
        Model.Tick();
        _forms.Tick(now);
    }

    /// <summary>Draws the setup over the whole canvas, scaled to its width.</summary>
    public void Draw(Canvas canvas)
    {
        _forms.Scale = Math.Max(1, canvas.Width / LayoutWidth);
        canvas.Clear(Color.Rgb(10, 12, 20));
        _forms.Draw(canvas);
    }

    public bool MouseMove(int x, int y) => _forms.MouseMove(x, y);

    public bool MouseDown(int x, int y) => _forms.MouseDown(x, y, false);

    public bool MouseUp(int x, int y) => _forms.MouseUp(x, y);

    public bool Wheel(int delta) => _forms.Wheel(delta);

    public bool Key(UiKey key, bool shift) => UiApp.ToFormKey(key) is { } k && _forms.Key(k, shift);

    public void Text(string text)
    {
        foreach (var c in text)
        {
            _forms.Text(c);
        }
    }

    // No game art yet: every skin falls back to plain bevels.
    private sealed class NoArt : IArtSource
    {
        public RgbaImage? Get(string name) => null;
    }
}
