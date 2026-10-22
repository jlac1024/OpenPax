using System.Globalization;
using OpenPax.Forms.Drawing;
using OpenPax.Forms.Input;
using OpenPax.Forms.Layout;

namespace OpenPax.Forms.Controls;

/// <summary>
/// A number with the game's green arrows either side of its box, as the
/// original's spinners: an arrow steps once and repeats while held (after
/// half a second, every 25 ms, as does), dragging across the box
/// changes the value a step per pixel, and Left, Right and the wheel step it.
/// </summary>
public sealed class NumericUpDown : Element
{
    private const int RepeatDelay = 500;
    private const int RepeatPeriod = 25;

    private int _value;
    private long _now;
    private long _nextRepeat;
    private int _dragValue;
    private int _dragX;

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

    private int ArrowWidth => Theme.ArrowLeft?.Width ?? 12;

    protected override Size MeasureOverride(Size available) => new(ArrowWidth * 2 + 28, Math.Max(13, Theme.ArrowLeft?.Height ?? 13));

    private int ArrowHeight => Theme.ArrowLeft?.Height ?? Bounds.Height;

    protected override void OnRender(DrawContext dc)
    {
        // Arrows and box all at the arrows' own height, centered in the row, so they line up as the original's do.
        var top = Bounds.Y + Math.Max(0, (Bounds.Height - ArrowHeight) / 2);
        var height = Math.Min(Bounds.Height, ArrowHeight);
        var left = new Rect(Bounds.X, top, ArrowWidth, height);
        var right = new Rect(Bounds.Right - ArrowWidth, top, ArrowWidth, height);
        var box = new Rect(left.Right, top, Math.Max(0, right.X - left.Right), height);
        dc.SkinOr(Theme.SpinBox, box, Theme.Well, true);
        Arrow(dc, left, IsPressed && Pressing < 0 ? Theme.ArrowLeftPressed ?? Theme.ArrowLeft : Theme.ArrowLeft);
        Arrow(dc, right, IsPressed && Pressing > 0 ? Theme.ArrowRightPressed ?? Theme.ArrowRight : Theme.ArrowRight);
        var text = _value.ToString(CultureInfo.InvariantCulture);
        var h = dc.LineHeight(Theme.DefaultFont);
        dc.PushClip(box);
        dc.Text(text, box.Right - 4 - dc.Measure(text, Theme.DefaultFont), box.Y + (box.Height - h) / 2, Theme.Text, Theme.DefaultFont);
        dc.PopClip();
        if (IsFocused)
        {
            dc.Outline(box, Theme.Accent.WithAlpha(120));
        }
    }

    private int Pressing { get; set; }

    private void Arrow(DrawContext dc, Rect r, Render.TrueColor.RgbaImage? image)
    {
        if (image is not null)
        {
            dc.ImageCentered(image, r);
        }
        else
        {
            dc.Fill(r, Theme.Light);
            dc.Bevel(r, false);
        }
    }

    protected internal override void OnMouseDown(int x, int y, bool right)
    {
        Pressing = x < Bounds.X + ArrowWidth ? -1 : x >= Bounds.Right - ArrowWidth ? 1 : 0;
        Value += Pressing * Step;
        _nextRepeat = _now + RepeatDelay;
        (_dragValue, _dragX) = (Value, x);
    }

    protected internal override void OnDoubleClick(int x, int y) => OnMouseDown(x, y, false);

    /// <summary>Dragging on the box: a step per unit across, from where the press began.</summary>
    protected internal override void OnMouseMove(int x, int y, bool pressed)
    {
        if (pressed && IsPressed && Pressing == 0)
        {
            Value = _dragValue + ((x - _dragX) * Step);
        }
    }

    protected internal override void OnMouseUp(int x, int y, bool over) => Pressing = 0;

    /// <summary>A held arrow repeats.</summary>
    protected internal override void OnTick(long now)
    {
        _now = now;
        while (IsPressed && Pressing != 0 && now >= _nextRepeat)
        {
            Value += Pressing * Step;
            _nextRepeat += RepeatPeriod;
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
            default:
                return false;
        }
    }
}
