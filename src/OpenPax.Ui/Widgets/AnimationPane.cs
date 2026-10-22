using OpenPax.Data.Views;
using OpenPax.Render;

namespace OpenPax.Ui.Widgets;

/// <summary>
/// Class 8037, a run of pictures played forward or backward at a fixed rate.
/// A play that does not loop sends the animation's id when it ends. It
/// never takes clicks.
/// </summary>
public sealed class AnimationPane(ViewNode node, int left, int top, AnimationDetails details) : Pane(node, left, top)
{
    private long _nextFrame;
    private bool _startPending;

    public AnimationDetails Details { get; } = details;

    public int Frame { get; private set; }

    public bool Playing { get; private set; }

    public bool Looping { get; private set; }

    public bool Reverse { get; private set; }

    /// <summary>Added to every frame's picture (+0x84), set by some screens.</summary>
    public int PictureOffset { get; set; }

    /// <summary>The frame period in milliseconds (1000 / fps; 40 when the data has none).</summary>
    public int Period => Details.Fps > 0 ? 1000 / Details.Fps : 40;

    private ImageSet Set => (ImageSet)Math.Clamp(Details.PictureFile, 0, 9);

    public override bool HitTest(int x, int y)
    {
        return false;
    }

    /// <summary>Plays forward from a frame.</summary>
    public void Play(int start, bool loop)
    {
        Frame = start;
        (Looping, Reverse, Playing) = (loop, false, true);
        _startPending = true;
    }

    /// <summary>Plays backward; a start of 0 means the last frame.</summary>
    public void PlayBackward(int start, bool loop)
    {
        Frame = start == 0 ? Details.Frames - 1 : start;
        (Looping, Reverse, Playing) = (loop, true, true);
        _startPending = true;
    }

    public void Stop()
    {
        Playing = false;
    }

    /// <summary>Shows one frame without playing.</summary>
    public void Show(int frame)
    {
        Frame = frame;
        Playing = false;
    }

    protected internal override void Tick(long now)
    {
        if (!Playing)
        {
            return;
        }

        if (_startPending)
        {
            _startPending = false;
            _nextFrame = now + Period;
            return;
        }

        while (Playing && now >= _nextFrame)
        {
            _nextFrame += Period;
            var next = Frame + (Reverse ? -1 : 1);
            if (next >= 0 && next < Details.Frames)
            {
                Frame = next;
            }
            else if (Looping)
            {
                Frame = Reverse ? Details.Frames - 1 : 0;
            }
            else
            {
                Playing = false;
                Post(Id, 0);
            }
        }
    }

    // frames are full rect art, drawn without erasing.
    protected override void Draw(Framebuffer target)
    {
        if (Assets.Pictures(Set).Get(PictureOffset + Details.FirstPicture + Frame) is { } sprite)
        {
            target.Draw(sprite, Left, Top);
        }
    }
}
