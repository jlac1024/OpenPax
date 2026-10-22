using OpenPax.Data.Views;
using OpenPax.Render;
using OpenPax.Sim.Players;

namespace OpenPax.Ui.Widgets;

/// <summary>
/// Class 18001, one research field's project: the tech's icon, its name
/// and a bar with the percent done. A click on a field with a project
/// sends the pane's id, then 1112 with the tech.
/// </summary>
public sealed class ResearchSlotPane(ViewNode node, int left, int top, ByteOptionDetails details) : Pane(node, left, top)
{
    /// <summary>Sent with the tech id when a project is clicked.</summary>
    public const int TechClicked = 1112;

    public ResearchSlot? Slot { get; set; }

    public Func<int, TechInfo>? TechById { get; set; }

    /// <summary>Framed as the project of the tech shown (+0x50).</summary>
    public bool Highlighted { get; set; }

    protected internal override void MouseUp(int x, int y)
    {
        if (Inside(x, y) && Slot is { Tech: not ResearchSlot.None } slot)
        {
            Post(Id, 0);
            Post(TechClicked, slot.Tech);
            Highlighted = true;
        }
    }

    protected override void Draw(Framebuffer target)
    {
        var main = Assets.Pictures(ImageSet.Main);
        if (Highlighted && main.Get(18013) is { } frame)
        {
            target.Draw(frame, Left, Top);
        }

        if (Slot is not { Tech: not ResearchSlot.None } slot || TechById is not { } techById)
        {
            return;
        }

        var font = Assets.Fonts.ForIndex(1);
        var color = details.Value;
        font.Draw(target, Assets.Strings.Get(new TextRef(100000 + slot.Tech, 0)), Left + 65, Top + 16, 0, 0, color, 0);

        // the bar fills by the share done, clipped from the full picture.
        var (x0, y0, x1, y1) = (Left + 65, Top + 27, Left + 188, Top + 51);
        var done = Math.Clamp(slot.Progress, 0, Math.Max(slot.Cost, 0));
        var percent = 100;
        if (slot.Cost != 0)
        {
            percent = done * 100 / slot.Cost;
            x1 = ((x1 - x0) * percent / 100) + 1 + x0;
        }

        if (main.Get(18014) is { } empty)
        {
            target.Draw(empty, x0 - 1, y0 - 1);
        }

        target.SetClip(x0, y0, x1 - x0, y1 - y0);
        if (main.Get(18012) is { } full)
        {
            target.Draw(full, x0, y0);
        }

        target.SetClip(Left, Top, Width, Height);
        var label = percent.ToString(System.Globalization.CultureInfo.InvariantCulture) + Assets.Strings.Get(new TextRef(109, 2))
            + Assets.Strings.Get(new TextRef(18202, 0));
        font.Draw(target, label, x0 + 3, y0 + 11, 0, 0, color, 0);

        // the icon is the picture before the animation's first.
        if (Assets.Pictures(ImageSet.Tech).Get(techById(slot.Tech).Picture - 1) is { } icon)
        {
            target.Draw(icon, Left + 3, Top + 3);
        }
    }
}
