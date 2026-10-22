using OpenPax.Forms.Drawing;
using OpenPax.Forms.Input;
using OpenPax.Forms.Layout;
using OpenPax.Render.TrueColor;

namespace OpenPax.Forms.Controls;

/// <summary>A box that is ticked or not, in the game's green check art, with its text to the right.</summary>
public class CheckBox : Element
{
    private bool _checked;
    private string _text = "";

    public bool IsChecked
    {
        get => _checked;
        set
        {
            if (Set(ref _checked, value))
            {
                CheckedChanged?.Invoke(this, EventArgs.Empty);
            }
        }
    }

    public string Text
    {
        get => _text;
        set => Set(ref _text, value ?? "");
    }

    public int? Font { get; set; }

    public override bool Focusable => true;

    public event EventHandler? CheckedChanged;

    protected virtual RgbaImage? On => Theme.CheckOn;

    protected virtual RgbaImage? Off => Theme.CheckOff;

    private int FontIndex => Font ?? Theme.DefaultFont;

    private int BoxSize => Math.Max(On?.Width ?? 0, Off?.Width ?? 10);

    protected override Size MeasureOverride(Size available)
    {
        var font = Theme.Fonts(FontIndex);
        var box = Math.Max(On?.Height ?? 0, Off?.Height ?? 10);
        return new Size(BoxSize + 4 + Theme.Measure(FontIndex, _text), Math.Max(box, Theme.LineHeight(FontIndex)));
    }

    protected override void OnRender(DrawContext dc)
    {
        var picture = _checked ? On : Off;
        var boxRect = new Rect(Bounds.X, Bounds.Y, BoxSize, Bounds.Height);
        if (picture is not null)
        {
            dc.ImageCentered(picture, boxRect);
        }
        else
        {
            var r = new Rect(Bounds.X, Bounds.Y + (Bounds.Height - 10) / 2, 10, 10);
            dc.Fill(r, Theme.Well);
            dc.Bevel(r, true);
            if (_checked)
            {
                dc.Fill(r.Deflate(new Thickness(2)), Theme.Accent);
            }
        }

        var h = dc.LineHeight(FontIndex);
        dc.Text(_text, Bounds.X + BoxSize + 4, Bounds.Y + (Bounds.Height - h) / 2, IsMouseOver ? Theme.Heading : Theme.PanelText, FontIndex);
    }

    protected internal override void OnMouseUp(int x, int y, bool over)
    {
        if (over)
        {
            Toggle();
        }
    }

    protected internal override bool OnKey(FormKey key, bool shift)
    {
        if (key == FormKey.Space)
        {
            Toggle();
            return true;
        }

        return false;
    }

    protected virtual void Toggle() => IsChecked = !IsChecked;
}
