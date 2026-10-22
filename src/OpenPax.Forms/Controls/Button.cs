using OpenPax.Forms.Drawing;
using OpenPax.Forms.Input;
using OpenPax.Forms.Layout;

namespace OpenPax.Forms.Controls;

/// <summary>A push button in the game's brushed-silver art, dark while pressed. Click fires on release over it, or on Return or Space when focused.</summary>
public class Button : Element
{
    private string _text = "";

    public string Text
    {
        get => _text;
        set => Set(ref _text, value ?? "");
    }

    public int? Font { get; set; }

    public override bool Focusable => true;

    public event EventHandler? Click;

    protected int FontIndex => Font ?? Theme.DefaultFont;

    protected override Size MeasureOverride(Size available) =>
        new(Theme.Measure(FontIndex, _text, true) + 24, Math.Max(17, Theme.LineHeight(FontIndex) + 6));

    protected override void OnRender(DrawContext dc)
    {
        var down = IsPressed && IsMouseOver;
        dc.SkinOr(down ? Theme.ButtonPressed ?? Theme.Button : Theme.Button, Bounds, Theme.Light, down);
        if (!dc.Enabled)
        {
            dc.Fill(Bounds, Theme.Shade.WithAlpha(120));
        }

        var w = dc.Measure(_text, FontIndex, true);
        var h = dc.LineHeight(FontIndex);
        dc.Text(_text, Bounds.X + (Bounds.Width - w) / 2 + 3, Bounds.Y + (Bounds.Height - h) / 2, down ? Theme.ButtonPressedText : Theme.ButtonText, FontIndex, true);
        if (IsFocused)
        {
            dc.Outline(Bounds, Theme.Accent.WithAlpha(160));
        }
    }

    protected internal override void OnMouseUp(int x, int y, bool over)
    {
        if (over)
        {
            OnClick();
        }
    }

    protected internal override bool OnKey(FormKey key, bool shift)
    {
        if (key is FormKey.Return or FormKey.Space)
        {
            OnClick();
            return true;
        }

        return false;
    }

    protected virtual void OnClick() => Click?.Invoke(this, EventArgs.Empty);
}
