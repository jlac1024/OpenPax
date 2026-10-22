using OpenPax.Data.Views;
using OpenPax.Render;

namespace OpenPax.Ui.Widgets;

/// <summary>
/// Class 8002, a push button or, with the latch byte, one that stays down
/// after a click until code releases it. A click sends the button's id.
/// Class 8039 adds an icon that plays its frames while the pointer is over
/// the button.
/// </summary>
public sealed class ButtonPane(ViewNode node, int left, int top, ButtonDetails details) : Pane(node, left, top)
{
    private const int IconFrameTime = 66;

    private bool _tracking;
    private bool _hovered;
    private long _now;
    private long _hoverStart;

    public ButtonDetails Details { get; } = details;

    public bool Pressed { get; set; }

    /// <summary>A picture set by code in place of the layout's (+0x58), or null.</summary>
    public int? Picture { get; set; }

    /// <summary>A text color set by code in place of the layout's (+0x6C), or null.</summary>
    public byte? Color { get; set; }

    /// <summary>The caption's entry in the button's STR# list, when code changes it (+0x54).</summary>
    public int? TextIndex { get; set; }

    /// <summary>A caption set by code in place of the STR# entry; the port's own screens use it.</summary>
    public string? Caption { get; set; }

    /// <summary>
    /// The port's own: the button's pictures are only copies of the art
    /// behind it, so they are left out. A button moved off the
    /// place its picture was cut for (Game Start's six, docs/ui/options.md)
    /// then shows the art where it now is, not a strip of somewhere else.
    /// </summary>
    public bool PictureIsBackground { get; set; }

    public bool Latching => Details.Latching != 0;

    // a latched button that is down ignores presses.
    protected internal override void MouseDown(int x, int y, int clicks)
    {
        _tracking = !Pressed;
        if (_tracking)
        {
            Pressed = Inside(x, y);
            PlaySound(Details.Sound);
        }
    }

    protected internal override void MouseMove(int x, int y, long now)
    {
        if (_tracking)
        {
            Pressed = Inside(x, y);
        }
    }

    protected internal override void MouseUp(int x, int y)
    {
        if (!_tracking)
        {
            return;
        }

        _tracking = false;
        var hit = Inside(x, y);
        Pressed = Latching && hit;
        if (hit)
        {
            PlaySound(Details.Sound == 0 ? 0 : Details.Sound + Audio.SoundIds.Release);
            Post(Id, 0);
        }
    }

    /// <summary>An animated button (class 8039): its icon's first picture and frame count.</summary>
    public bool Animated => Details.AnimFirstPicture is not null;

    /// <summary>The icon frame shown: 0 until hovered, then one per 66 ms, looping.</summary>
    public int IconFrame => _hovered && Details.AnimFrames is > 0 and var frames ? (int)((_now - _hoverStart) / IconFrameTime % frames) : 0;

    protected internal override void Tick(long now)
    {
        _now = now;
    }

    protected internal override void MouseWithin(int x, int y)
    {
        if (!_hovered)
        {
            _hovered = true;
            _hoverStart = _now;
        }

        Post(UiMessage.Within, Id);
    }

    protected internal override void MouseLeft()
    {
        _hovered = false;
        Post(UiMessage.Left, 0);
    }

    protected override void Draw(Framebuffer target)
    {
        var pictures = Assets.Pictures(ImageSet.Main);
        if (Animated)
        {
            DrawAnimated(target, pictures);
            return;
        }

        var picture = Picture ?? Details.Picture;
        if (picture != 0 && PictureIsBackground)
        {
            // The art behind shows through, as it is wherever the button is.
            // The pressed (+100) and disabled (+200) pictures are the same
            // strip of art from where the button was, so they are left out
            // too: a press shows in the caption's offset and color.
        }
        else if (picture != 0)
        {
            // A disabled button without its own picture (+200) shows the plain one.
            var sprite = !Enabled
                ? pictures.Get(picture + 200) ?? pictures.Get(picture)
                : pictures.Get(picture + (Pressed ? 100 : 0));
            if (sprite is not null)
            {
                // Widened in a wide window (docs/ui/widescreen.md): the middle third of the picture stretches.
                if (Width > sprite.Width)
                {
                    WideDraw.Draw(target, sprite, Left, Top, new WideLayout(sprite.Width / 3, sprite.Width / 3), Width - sprite.Width);
                }
                else
                {
                    target.Draw(sprite, Left, Top);
                }
            }
        }
        else
        {
            DrawBevel(target);
        }

        var text = Caption ?? Assets.Strings.Get(TextIndex is { } index ? Details.Text with { Index = index } : Details.Text);
        if (text.Length == 0)
        {
            return;
        }

        // A widened button's caption keeps its place: centered or right aligned text moves with the width.
        var grown = Original is { } o ? Width - o.Width : 0;
        var x = Left + Details.TextX + (Details.Align == 1 ? grown / 2 : Details.Align == 2 ? grown : 0);
        var y = Top + Details.TextY;
        if (Pressed)
        {
            x += Details.PressX;
            y += Details.PressY;
        }

        var color = !Enabled ? Details.DisabledColor : Pressed ? Details.PressedColor : Color ?? Details.Color;
        Assets.Fonts.ForIndex(Details.Font).Draw(target, text, x, y, Details.Style, Details.Align, color, Details.ShadowColor);
    }

    /// <summary>
    /// The slot picture (+100 while pressed; there is no disabled picture),
    /// then the icon frame at the button's second offset, which plain
    /// buttons use to move their text when pressed (object +0x64).
    /// </summary>
    private void DrawAnimated(Framebuffer target, PictureLibrary pictures)
    {
        if (Details.Picture != 0 && pictures.Get(Details.Picture + (Pressed ? 100 : 0)) is { } slot)
        {
            target.Draw(slot, Left, Top);
        }

        if (pictures.Get((Details.AnimFirstPicture ?? 0) + IconFrame) is { } icon)
        {
            target.Draw(icon, Left + Details.PressX, Top + Details.PressY);
        }
    }

    // Placeholder bevel for buttons without a picture; none of the ported screens has one.
    private void DrawBevel(Framebuffer target)
    {
        var light = Pressed ? (byte)0 : (byte)255;
        var dark = Pressed ? (byte)255 : (byte)0;
        target.FillRect(Left, Top, Width, 1, light);
        target.FillRect(Left, Top, 1, Height, light);
        target.FillRect(Left, Top + Height - 1, Width, 1, dark);
        target.FillRect(Left + Width - 1, Top, 1, Height, dark);
    }
}
