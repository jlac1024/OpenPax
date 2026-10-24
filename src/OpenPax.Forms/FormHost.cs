using OpenPax.Forms.Drawing;
using OpenPax.Forms.Input;
using OpenPax.Forms.Layout;
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

    /// <summary>
    /// A tooltip of a few lines by a point in canvas pixels, kept inside the
    /// canvas: the first line bold as its heading, the rest wrapped to a
    /// readable width ('\n' starts a new paragraph).
    /// </summary>
    public void DrawTip(Canvas canvas, string text, int x, int y)
    {
        Theme.Scale = Scale;
        var dc = new DrawContext(canvas, Theme, Scale, now: Now);
        var font = Theme.DefaultFont;
        const int MaxWidth = 220;
        var paragraphs = text.Split('\n');
        var lines = new List<(string Text, bool Bold)>();
        for (var p = 0; p < paragraphs.Length; p++)
        {
            var line = "";
            foreach (var word in paragraphs[p].Split(' ', StringSplitOptions.RemoveEmptyEntries))
            {
                var next = line.Length == 0 ? word : line + " " + word;
                if (line.Length > 0 && dc.Measure(next, font, p == 0) > MaxWidth)
                {
                    lines.Add((line, p == 0));
                    line = word;
                }
                else
                {
                    line = next;
                }
            }

            lines.Add((line, p == 0));
        }

        var lineHeight = dc.LineHeight(font);
        var w = lines.Max(l => dc.Measure(l.Text, font, l.Bold)) + 8;
        var h = (lines.Count * lineHeight) + 6;
        var (width, height) = (canvas.Width / Scale, canvas.Height / Scale);
        var left = Math.Clamp((x / Scale) + 12, 0, Math.Max(0, width - w));
        var top = (y / Scale) + 16 + h <= height ? (y / Scale) + 16 : Math.Max(0, (y / Scale) - h - 4);
        var box = new Rect(left, top, w, h);
        dc.Fill(box, Theme.Well);
        dc.Outline(box, Theme.Accent);
        for (var i = 0; i < lines.Count; i++)
        {
            dc.Text(lines[i].Text, left + 4, top + 3 + (i * lineHeight), Theme.Text, font, lines[i].Bold);
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
