using OpenPax.Forms.Drawing;
using OpenPax.Forms.Input;
using OpenPax.Render.TrueColor;

namespace OpenPax.Forms;

/// <summary>
/// Shows forms on the true-color canvas: the top one takes the input, all
/// are drawn, bottom first. Layout units are canvas pixels divided by the
/// scale, so a form fills any window and the art stays sharp.
/// </summary>
public sealed class FormHost(Theme theme)
{
    private readonly List<Form> _forms = [];

    public Theme Theme { get; } = theme;

    /// <summary>Canvas pixels per layout unit, a whole number.</summary>
    public int Scale { get; set; } = 1;

    public IReadOnlyList<Form> Forms => _forms;

    public Form? Top => _forms.Count > 0 ? _forms[^1] : null;

    public void Show(Form form)
    {
        form.Theme = Theme;
        _forms.Remove(form);
        _forms.Add(form);
    }

    public void Close(Form form) => _forms.Remove(form);

    /// <summary>Whether the form is shown (not closed yet).</summary>
    public bool Contains(Form form) => _forms.Contains(form);

    public void Clear() => _forms.Clear();

    /// <summary>The time of the last tick, which drawing animates by.</summary>
    public long Now { get; private set; }

    public void Tick(long now)
    {
        Now = now;
        foreach (var f in _forms.ToList())
        {
            f.Tick(now);
        }
    }

    /// <summary>Lays out and draws every form over the canvas.</summary>
    public void Draw(Canvas canvas)
    {
        Theme.Scale = Scale;
        var dc = new DrawContext(canvas, Theme, Scale, now: Now);
        foreach (var f in _forms)
        {
            f.Layout(canvas.Width / Scale, canvas.Height / Scale);
            f.Draw(dc);
        }
    }

    // Input arrives in canvas pixels; each returns whether a form took it.
    public bool MouseMove(int x, int y) => Top?.MouseMove(x / Scale, y / Scale) ?? false;

    public bool MouseDown(int x, int y, bool right) => Top?.MouseDown(x / Scale, y / Scale, right) ?? false;

    public bool MouseUp(int x, int y) => Top?.MouseUp(x / Scale, y / Scale) ?? false;

    public bool Wheel(int delta) => Top?.Wheel(delta) ?? false;

    public bool Key(FormKey key, bool shift) => Top?.Key(key, shift) ?? false;

    public bool Text(char c) => Top?.Text(c) ?? false;
}
