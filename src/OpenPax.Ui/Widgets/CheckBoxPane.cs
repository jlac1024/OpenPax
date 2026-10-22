using OpenPax.Data.Views;
using OpenPax.Render;

namespace OpenPax.Ui.Widgets;

/// <summary>
/// Class 8016, a box and its text. A click flips it and sends the box's id
/// with the new value (1 checked, 0 not).
/// </summary>
public sealed class CheckBoxPane(ViewNode node, int left, int top, CheckBoxDetails details) : Pane(node, left, top)
{
    private bool _tracking;
    private bool _shown;

    public CheckBoxDetails Details { get; } = details;

    public bool Checked { get; private set; }

    /// <summary>Text set by code in place of the STR# entry, if any.</summary>
    public string? Text { get; set; }

    public void Set(bool value)
    {
        Checked = value;
        _shown = value;
    }

    protected internal override void MouseDown(int x, int y, int clicks)
    {
        _tracking = true;
        _shown = !Checked;
        PlaySound(Details.Sound);
    }

    protected internal override void MouseMove(int x, int y, long now)
    {
        if (_tracking)
        {
            _shown = Inside(x, y) ? !Checked : Checked;
        }
    }

    protected internal override void MouseUp(int x, int y)
    {
        if (!_tracking)
        {
            return;
        }

        _tracking = false;
        if (Inside(x, y))
        {
            Set(!Checked);
            PlaySound(Details.Sound == 0 ? 0 : Details.Sound + Audio.SoundIds.Release);
            Post(Id, Checked ? 1 : 0);
        }
        else
        {
            _shown = Checked;
        }
    }

    // a disabled box always shows unchecked, with no picture of its own.
    protected override void Draw(Framebuffer target)
    {
        var on = _shown && Enabled;
        var width = 0;
        if (Assets.Pictures(ImageSet.Main).Get(Details.Picture + (on ? 100 : 0)) is { } sprite)
        {
            target.Draw(sprite, Left, Top);
            width = sprite.Width;
        }

        var text = Text ?? Assets.Strings.Get(Details.Text);
        var font = Assets.Fonts.ForIndex(Details.Font);
        var x = TextPlacement.AnchorX(Details.Align, Left + width, Width - width);
        var y = Top + Height - (font.LineHeight - font.AscentFor(Details.Style) + 1);
        font.Draw(target, text, x, y, Details.Style, Details.Align, on ? Details.CheckedColor : Details.Color, 0);
    }
}
