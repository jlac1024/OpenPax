using OpenPax.Data.Views;
using OpenPax.Render;

namespace OpenPax.Ui.Widgets;

/// <summary>
/// Class 8027, a value with a decrease arrow, a box and an increase arrow,
/// side by side from the widget's left. Arrows step by one, repeating while
/// held; dragging on the box changes the value by one per pixel. Listeners
/// hear the value only on release.
/// </summary>
public sealed class SpinnerPane(ViewNode node, int left, int top, SpinnerDetails details) : Pane(node, left, top)
{
    private const int RepeatDelay = 500;
    private const int RepeatPeriod = 25;

    private enum Part
    {
        None,
        Increase,
        Decrease,
        Box,
    }

    private Part _held;
    private bool _heldOver;
    private long _nextRepeat;
    private int _dragValue;
    private int _dragX;

    public SpinnerDetails Details { get; } = details;

    public int Value { get; set; } = details.Range.Value;

    public int Min { get; set; } = details.Range.Min;

    public int Max { get; set; } = details.Range.Max;

    /// <summary>A STR# list that names the values (entry |min| + value), or 0 to show the number (+0xC8).</summary>
    public int NamesList { get; set; }

    /// <summary>The number is followed by "% " (byte +0xB6).</summary>
    public bool PercentSuffix { get; set; }

    /// <summary>The message sent on release: the VIEW's change message, the spinner's id.</summary>
    public int Message => Details.Range.ChangeMessage;

    private (int Left, int Width) Decrease => (Left, PictureWidth(Details.DecreasePicture));

    private (int Left, int Width) Box => (Left + Decrease.Width, PictureWidth(Details.BackgroundPicture));

    private (int Left, int Width) Increase => (Box.Left + Box.Width, PictureWidth(Details.IncreasePicture));

    private int PictureWidth(int picture)
    {
        return Assets.Pictures(ImageSet.Main).Get(picture)?.Width ?? 0;
    }

    private Part PartAt(int x, int y)
    {
        bool In((int Left, int Width) r) => x > r.Left && x < r.Left + r.Width && y > Top && y < Top + Height;
        return In(Decrease) ? Part.Decrease : In(Increase) ? Part.Increase : In(Box) ? Part.Box : Part.None;
    }

    /// <summary>Over the value box the pointer becomes the slide pointer, which drags the value.</summary>
    protected internal override void MouseWithin(int x, int y)
    {
        Assets.Cursor.Show(PartAt(x, y) == Part.Box ? MouseCursor.Slide : MouseCursor.Arrow);
    }

    protected internal override void MouseLeft()
    {
        Assets.Cursor.Show(MouseCursor.Arrow);
    }

    protected internal override void MouseDown(int x, int y, int clicks)
    {
        _held = PartAt(x, y);
        _heldOver = true;
        if (_held is Part.Increase or Part.Decrease)
        {
            Step(_held);
            _nextRepeat = long.MinValue;
        }
        else if (_held == Part.Box)
        {
            (_dragValue, _dragX) = (Value, x);
        }
    }

    protected internal override void MouseMove(int x, int y, long now)
    {
        switch (_held)
        {
            case Part.Box:
                var dragged = Math.Clamp(_dragValue + (x - _dragX), Min, Max);
                Sound(dragged - Value);
                Value = dragged;
                break;
            case Part.Increase or Part.Decrease:
                _heldOver = PartAt(x, y) == _held;
                if (_nextRepeat == long.MinValue)
                {
                    _nextRepeat = now + RepeatDelay;
                }
                else if (_heldOver && now >= _nextRepeat)
                {
                    Step(_held);
                    _nextRepeat = now + RepeatPeriod;
                }

                break;
        }
    }

    private void Step(Part part)
    {
        var next = Math.Clamp(Value + (part == Part.Increase ? 1 : -1), Min, Max);
        Sound(next - Value);
        Value = next;
    }

    /// <summary>Its sound on a step up, the sound plus 100 on a step down, none at a limit.</summary>
    private void Sound(int change)
    {
        if (change != 0 && Details.Sound != 0)
        {
            PlaySound(change > 0 ? Details.Sound : Details.Sound + Audio.SoundIds.Release);
        }
    }

    protected internal override void MouseUp(int x, int y)
    {
        if (_held == Part.None)
        {
            return;
        }

        _held = Part.None;
        Post(Message, Value);
    }

    protected override void Draw(Framebuffer target)
    {
        var pictures = Assets.Pictures(ImageSet.Main);
        Sprite? Pick(int picture, bool pressed) => !Enabled
            ? pictures.Get(picture + 200) ?? pictures.Get(picture)
            : pictures.Get(picture + (pressed ? 100 : 0)) ?? pictures.Get(picture);

        if (Pick(Details.DecreasePicture, _held == Part.Decrease && _heldOver) is { } dec)
        {
            target.Draw(dec, Decrease.Left, Top);
        }

        if (Pick(Details.BackgroundPicture, false) is { } box)
        {
            target.Draw(box, Box.Left, Top);
        }

        if (Pick(Details.IncreasePicture, _held == Part.Increase && _heldOver) is { } inc)
        {
            target.Draw(inc, Increase.Left, Top);
        }

        if (!Enabled)
        {
            return;
        }

        var text = NamesList != 0
            ? Assets.Strings.Get(new TextRef(NamesList, Math.Abs(Min) + Value))
            : Value.ToString(System.Globalization.CultureInfo.InvariantCulture) + (PercentSuffix ? Assets.Strings.Get(new TextRef(109, 2)) : "");

        // Open: the text is taken as centered on the widget; still to be checked.
        var font = Assets.Fonts.ForIndex(0x80);
        var baseline = Top + Height / 2 + font.LineHeight / 2 - (font.LineHeight - font.AscentFor(0)) + 1;
        font.Draw(target, text, Left + Width / 2, baseline, 0, 1, Details.Color, 0);
    }
}
