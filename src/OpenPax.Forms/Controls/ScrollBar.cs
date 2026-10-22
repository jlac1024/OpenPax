using OpenPax.Forms.Drawing;
using OpenPax.Forms.Layout;

namespace OpenPax.Forms.Controls;

/// <summary>
/// An up and down scroll bar in the game's art: a track with its arrows at
/// the ends and a green thumb sized to the part shown. Value runs from 0
/// to Maximum (what does not fit).
/// </summary>
public sealed class ScrollBar : Element
{
    public const int Thickness = 13;
    private const int ArrowLength = 12;

    private int _value;
    private int? _dragFrom;
    private int _dragValue;

    public int Value
    {
        get => _value;
        set
        {
            if (Set(ref _value, Math.Clamp(value, 0, Maximum)))
            {
                Scrolled?.Invoke(this, EventArgs.Empty);
            }
        }
    }

    public int Maximum { get; set; }

    /// <summary>How much is shown at once, which sizes the thumb and is a page step.</summary>
    public int Viewport { get; set; } = 1;

    public int SmallStep { get; set; } = 8;

    public event EventHandler? Scrolled;

    protected override Size MeasureOverride(Size available) => new(Thickness, ArrowLength * 2 + 8);

    private Rect Track => new(Bounds.X, Bounds.Y + ArrowLength, Bounds.Width, Math.Max(1, Bounds.Height - ArrowLength * 2));

    private Rect Thumb
    {
        get
        {
            var t = Track;
            var length = Maximum <= 0 ? t.Height : Math.Max(7, t.Height * Viewport / (Viewport + Maximum));
            var y = Maximum <= 0 ? t.Y : t.Y + (t.Height - length) * Value / Maximum;
            return new Rect(t.X, y, t.Width, length);
        }
    }

    protected override void OnRender(DrawContext dc)
    {
        if (Theme.ScrollTrack is { } track)
        {
            dc.Skin(track, Bounds);
        }
        else
        {
            dc.Fill(Bounds, Theme.Shade);
            dc.Bevel(Bounds, true);
        }

        if (Maximum > 0)
        {
            dc.SkinOr(Theme.ScrollThumb, Thumb, Theme.Accent, false);
        }
    }

    protected internal override void OnMouseDown(int x, int y, bool right)
    {
        var thumb = Thumb;
        if (y < Bounds.Y + ArrowLength)
        {
            Value -= SmallStep;
        }
        else if (y >= Bounds.Bottom - ArrowLength)
        {
            Value += SmallStep;
        }
        else if (thumb.Contains(x, y))
        {
            (_dragFrom, _dragValue) = (y, Value);
        }
        else
        {
            Value += y < thumb.Y ? -Viewport : Viewport;
        }
    }

    protected internal override void OnMouseMove(int x, int y, bool pressed)
    {
        if (pressed && _dragFrom is { } from && Maximum > 0)
        {
            var room = Math.Max(1, Track.Height - Thumb.Height);
            Value = _dragValue + (y - from) * Maximum / room;
        }
    }

    protected internal override void OnMouseUp(int x, int y, bool over) => _dragFrom = null;

    protected internal override bool OnWheel(int delta)
    {
        Value -= delta * SmallStep;
        return Maximum > 0;
    }
}
