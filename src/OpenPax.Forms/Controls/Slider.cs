using OpenPax.Forms.Drawing;
using OpenPax.Forms.Input;
using OpenPax.Forms.Layout;

namespace OpenPax.Forms.Controls;

/// <summary>A value between Minimum and Maximum, dragged along a sunken track with a silver handle; Left and Right step it.</summary>
public sealed class Slider : Element
{
    private const int HandleWidth = 8;

    private int _value;

    public int Minimum { get; set; }

    public int Maximum { get; set; } = 100;

    public int Step { get; set; } = 1;

    public int Value
    {
        get => _value;
        set
        {
            if (Set(ref _value, Math.Clamp(value, Minimum, Math.Max(Minimum, Maximum))))
            {
                ValueChanged?.Invoke(this, EventArgs.Empty);
            }
        }
    }

    public override bool Focusable => true;

    public event EventHandler? ValueChanged;

    protected override Size MeasureOverride(Size available) => new(100, 14);

    private int Room => Math.Max(1, Bounds.Width - HandleWidth);

    protected override void OnRender(DrawContext dc)
    {
        var track = new Rect(Bounds.X, Bounds.Y + Bounds.Height / 2 - 2, Bounds.Width, 4);
        dc.Fill(track, Theme.Well);
        dc.Bevel(track, true);
        var span = Math.Max(1, Maximum - Minimum);
        var filled = Room * (Value - Minimum) / span;
        dc.Fill(new Rect(track.X + 1, track.Y + 1, filled, 2), Theme.Accent);
        var handle = new Rect(Bounds.X + filled, Bounds.Y, HandleWidth, Bounds.Height);
        dc.SkinOr(IsPressed ? Theme.ButtonPressed ?? Theme.Button : Theme.Button, handle, Theme.Light, IsPressed);
        if (IsFocused)
        {
            dc.Outline(handle, Theme.Accent);
        }
    }

    protected internal override void OnMouseDown(int x, int y, bool right) => Drag(x);

    protected internal override void OnMouseMove(int x, int y, bool pressed)
    {
        if (pressed)
        {
            Drag(x);
        }
    }

    protected internal override bool OnWheel(int delta)
    {
        Value += delta * Step;
        return true;
    }

    protected internal override bool OnKey(FormKey key, bool shift)
    {
        switch (key)
        {
            case FormKey.Left or FormKey.Down:
                Value -= Step;
                return true;
            case FormKey.Right or FormKey.Up:
                Value += Step;
                return true;
            case FormKey.Home:
                Value = Minimum;
                return true;
            case FormKey.End:
                Value = Maximum;
                return true;
            default:
                return false;
        }
    }

    private void Drag(int x)
    {
        var raw = Minimum + (x - Bounds.X - HandleWidth / 2) * (Maximum - Minimum) / Room;
        Value = Minimum + (raw - Minimum + Step / 2) / Math.Max(1, Step) * Math.Max(1, Step);
    }
}
