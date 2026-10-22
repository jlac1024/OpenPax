using OpenPax.Data.Views;
using OpenPax.Render;

namespace OpenPax.Ui.Widgets;

/// <summary>
/// Class 8033, a scroll bar for a list, upright or lying: an arrow at each
/// end, a track and a thumb. Arrows step a row and repeat after 500 ms, then
/// every 100 ms while held; pressing the track jumps the thumb to the
/// pointer and drags it.
/// </summary>
public sealed class ScrollBarPane(ViewNode node, int left, int top, ScrollBarDetails details) : Pane(node, left, top)
{
    private const int RepeatDelay = 500;
    private const int RepeatPeriod = 100;

    private int _held;
    private long _lastStep;
    private int _delay = RepeatDelay;

    public ScrollBarDetails Details { get; } = details;

    private ListBoxPane? List => Window?.Get(Details.Target) as ListBoxPane;

    private Sprite? Picture(int id) => Assets.Pictures(ImageSet.Main).Get(id);

    private bool Upright => Details.Vertical != 0;

    private int Along(int x, int y) => Upright ? y : x;

    private int Length(Sprite? sprite, int fallback) => sprite is null ? fallback : Upright ? sprite.Height : sprite.Width;

    private int ArrowHeight => Length(Picture(Details.DecreasePicture), 11);

    private int Start => Upright ? Top : Left;

    private int TrackTop => Start + ArrowHeight;

    private int TrackBottom => Start + (Upright ? Height : Width) - ArrowHeight;

    private int ThumbHeight => Length(Picture(Details.ThumbPicture), 5);

    private void DrawAt(Framebuffer target, Sprite sprite, int along)
    {
        if (Upright)
        {
            target.Draw(sprite, Left, along);
        }
        else
        {
            target.Draw(sprite, along, Top);
        }
    }

    protected internal override void MouseDown(int x, int y, int clicks)
    {
        if (List is not { Scrollable: true } list)
        {
            return;
        }

        y = Along(x, y);
        if (y < TrackTop)
        {
            _held = 1;
            list.Step(-1);
        }
        else if (y >= TrackBottom)
        {
            _held = 2;
            list.Step(1);
        }
        else
        {
            _held = 3;
            Drag(list, y);
        }

        _lastStep = long.MinValue;
    }

    protected internal override void MouseMove(int x, int y, long now)
    {
        if (List is not { } list || _held == 0)
        {
            return;
        }

        y = Along(x, y);

        if (_held == 3)
        {
            Drag(list, y);
            return;
        }

        if (_lastStep == long.MinValue)
        {
            _lastStep = now;
        }

        var over = _held == 1 ? y < TrackTop : y >= TrackBottom;
        if (over && now - _lastStep >= _delay)
        {
            list.Step(_held == 1 ? -1 : 1);
            _lastStep = now;
            _delay = RepeatPeriod;
        }
    }

    protected internal override void MouseUp(int x, int y)
    {
        _held = 0;
        _delay = RepeatDelay;
    }

    private void Drag(ListBoxPane list, int y)
    {
        var span = TrackBottom - ThumbHeight - TrackTop;
        list.SetFraction(span <= 0 ? 0.0 : Math.Clamp((double)(y - TrackTop) / span, 0.0, 1.0));
    }

    protected override void Draw(Framebuffer target)
    {
        var scrollable = List?.Scrollable ?? false;
        var track = (scrollable ? null : Picture(Details.TrackPicture + 50)) ?? Picture(Details.TrackPicture);
        if (track is not null)
        {
            // A bar grown in a large window (docs/ui/widescreen.md) stretches its track to its length.
            if (Grown > 0 || GrownHeight > 0)
            {
                target.DrawRegion(track, 0, 0, track.Width, track.Height, Left, Top, track.Width + Grown, track.Height + GrownHeight);
            }
            else
            {
                target.Draw(track, Left, Top);
            }
        }

        Sprite? Arrow(int id, bool held) => !scrollable ? Picture(id + 200) ?? Picture(id) : Picture(id + (held ? 100 : 0)) ?? Picture(id);
        if (Arrow(Details.DecreasePicture, _held == 1) is { } up)
        {
            DrawAt(target, up, Start);
        }

        if (Arrow(Details.IncreasePicture, _held == 2) is { } down)
        {
            DrawAt(target, down, TrackBottom + ArrowHeight - Length(down, 11));
        }

        var thumb = Picture(Details.ThumbPicture + (_held != 0 ? 100 : 0)) ?? Picture(Details.ThumbPicture);
        if (scrollable && List is { } list && thumb is not null)
        {
            DrawAt(target, thumb, TrackTop + (int)((TrackBottom - ThumbHeight - TrackTop) * list.Fraction));
        }
    }
}
