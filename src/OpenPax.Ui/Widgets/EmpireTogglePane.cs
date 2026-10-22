using OpenPax.Data.Views;
using OpenPax.Render;
using OpenPax.Sim.Players;

namespace OpenPax.Ui.Widgets;

/// <summary>
/// Class 8042, a player's on/off toggle: a frame, the player's flag
/// (crossed once out), and either a box in the player's graph color or a
/// mark for the relation. Pressing flips it at once; letting go outside
/// flips it back, inside sends the id with the new value.
/// </summary>
// draw, press, move, release
public sealed class EmpireTogglePane(ViewNode node, int left, int top, EmpireToggleDetails details) : Pane(node, left, top)
{
    private bool _pressed;
    private bool _shown;

    public EmpireToggleDetails Details { get; } = details;

    /// <summary>The player, or null for an unused toggle (+0x4C).</summary>
    public Player? Player { get; set; }

    /// <summary>The viewer, for the relation mark.</summary>
    public Player? Viewer { get; set; }

    /// <summary>Show the relation mark instead of the color box (+0x60).</summary>
    public bool RelationMark { get; set; }

    public bool On { get; private set; }

    public void Set(bool on)
    {
        On = on;
        _shown = on;
    }

    protected internal override void MouseWithin(int x, int y)
    {
        Post(UiMessage.Within, Id);
    }

    protected internal override void MouseLeft()
    {
        Post(UiMessage.Left, 0);
    }

    protected internal override void MouseDown(int x, int y, int clicks)
    {
        _pressed = true;
        Set(!On);
        PlaySound(Details.Sound);
    }

    protected internal override void MouseMove(int x, int y, long now)
    {
        if (_pressed)
        {
            _shown = Inside(x, y) ? On : !On;
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
            PlaySound(Details.Sound == 0 ? 0 : Details.Sound + Audio.SoundIds.Release);
            Post(Id, On ? 1 : 0);
            Set(On);
        }
        else
        {
            Set(!On);
        }
    }

    protected override void Draw(Framebuffer target)
    {
        var main = Assets.Pictures(ImageSet.Main);
        if (main.Get(Details.FramePicture + (_shown ? 100 : 0)) is { } frame)
        {
            target.Draw(frame, Left, Top);
        }

        if (Player is not { } player)
        {
            return;
        }

        var d = _shown ? 1 : 0;
        if (Assets.Pictures(ImageSet.Empire).Get(player.Flag + 1000) is { } flag)
        {
            target.Draw(flag, Left + 8 + d, Top + 5 + d);
        }

        if (player.Eliminated && Assets.Pictures(ImageSet.Infra).Get(20) is { } cross)
        {
            target.Draw(cross, Left + 8 + d, Top + 5 + d);
        }

        if (!RelationMark)
        {
            // An outline, 28 by 18, in the player's line color.
            var (l, t, r, b) = (Left + 4 + d, Top + 3 + d, Left + 32 + d, Top + 21 + d);
            var color = PlayerColors.For(player.Index);
            target.DrawLine(l, t, r, t, color);
            target.DrawLine(l, t, l, b, color);
            target.DrawLine(l, b - 1, r, b - 1, color);
            target.DrawLine(r - 1, t, r - 1, b, color);
            return;
        }

        var level = Viewer is null || player.Eliminated ? Treaty.None : Viewer.Treaties[player.Index].Level();
        int? mark = level switch
        {
            Treaty.None => null,
            < 1 => 15501,
            <= 3 => 15503,
            _ => 15505,
        };
        if (mark is { } id && main.Get(id) is { } sprite)
        {
            target.Draw(sprite, Left + 4 + d, Top + 3 + d);
        }
    }
}
