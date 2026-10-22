using OpenPax.Data.Views;

namespace OpenPax.Ui.Widgets;

/// <summary>
/// Class 8021, an invisible clickable area. Quirk kept: it counts as
/// enabled whether shown or not, so hidden hot spots still
/// take clicks; disabling one removes it.
/// </summary>
public sealed class HotSpotPane(ViewNode node, int left, int top, HotSpotDetails details) : Pane(node, left, top)
{
    private bool _inside;
    private int _restore;

    public HotSpotDetails Details { get; } = details;

    public override bool HitTest(int x, int y)
    {
        return Enabled && (Parent?.Shown ?? true) && Inside(x, y);
    }

    /// <summary>A hot spot with a pointer of its own shows it while the mouse is over it, keeping the one it replaced.</summary>
    protected internal override void MouseWithin(int x, int y)
    {
        if (Details.Cursor != 0 && Assets.Cursor.Set != Details.Cursor)
        {
            _restore = Assets.Cursor.Set;
            Assets.Cursor.Show(Details.Cursor);
        }

        Post(UiMessage.Within, Id);
    }

    protected internal override void MouseLeft()
    {
        if (_restore != 0)
        {
            Assets.Cursor.Show(_restore);
        }

        Post(UiMessage.Left, Id);
    }

    protected internal override void MouseDown(int x, int y, int clicks)
    {
        Post(UiMessage.HotSpotPressed, Id);
        PlaySound(Details.Sound);
        _inside = true;
    }

    protected internal override void MouseMove(int x, int y, long now)
    {
        var inside = Inside(x, y);
        if (inside != _inside)
        {
            _inside = inside;
            Post(inside ? UiMessage.HotSpotPressed : UiMessage.HotSpotReleased, Id);
        }
    }

    protected internal override void MouseUp(int x, int y)
    {
        if (_inside)
        {
            Post(UiMessage.HotSpotReleased, Id);
        }

        if (Inside(x, y))
        {
            PlaySound(Details.Sound == 0 ? 0 : Details.Sound + Audio.SoundIds.Release);
            Post(Id, 0);
        }

        _inside = false;
    }
}
