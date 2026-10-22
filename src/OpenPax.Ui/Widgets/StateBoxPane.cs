using OpenPax.Data.Views;
using OpenPax.Render;

namespace OpenPax.Ui.Widgets;

/// <summary>
/// Class 8035, a box with four states: off (the picture), in force
/// (picture + 200) and pending either way (picture + 100). A click previews
/// the next state while held and sends the box's id with the old value at
/// release; the window then sets the real state from the game.
/// </summary>
public sealed class StateBoxPane(ViewNode node, int left, int top, StateBoxDetails details) : Pane(node, left, top)
{
    private bool _pressed;
    private int _next;
    private int _shown;

    public StateBoxDetails Details { get; } = details;

    /// <summary>0 off, 1 in force, 2 offered by me, 3 offered to me.</summary>
    public int Value { get; private set; }

    /// <summary>Sets the state, if it differs.</summary>
    public void Set(int value)
    {
        if (value != Value)
        {
            Value = value;
            _next = value;
            _shown = value;
        }
    }

    // Quirk kept: off previews as pending even for War,
    // which is declared at once.
    protected internal override void MouseDown(int x, int y, int clicks)
    {
        _pressed = true;
        _next = Value switch
        {
            0 => 2,
            1 or 2 => 0,
            _ => Value,
        };
        _shown = _next;
        PlaySound(Details.Sound);
    }

    protected internal override void MouseMove(int x, int y, long now)
    {
        if (_pressed)
        {
            _shown = Inside(x, y) ? _next : Value;
        }
    }

    protected internal override void MouseUp(int x, int y)
    {
        if (!_pressed)
        {
            return;
        }

        _pressed = false;
        if (Inside(x, y))
        {
            var old = Value;
            Value = _next;
            PlaySound(Details.Sound == 0 ? 0 : Details.Sound + Audio.SoundIds.Release);
            Post(Id, old);
        }
        else
        {
            _shown = Value;
        }
    }

    protected override void Draw(Framebuffer target)
    {
        var picture = Details.Picture + _shown switch
        {
            1 => 200,
            2 or 3 => 100,
            _ => 0,
        };
        if (Assets.Pictures(ImageSet.Main).Get(picture) is { } sprite)
        {
            target.Draw(sprite, Left, Top);
        }

        var font = Assets.Fonts.ForIndex(1);
        var baseline = TextPlacement.Baseline(font, Details.Style, Top + Height);
        font.Draw(target, Assets.Strings.Get(Details.Text), Left + 20, baseline, Details.Style, 0, Enabled ? Details.Color : Details.DisabledColor, 0);
    }
}
