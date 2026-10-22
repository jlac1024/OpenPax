using OpenPax.Data.Views;
using OpenPax.Render;
using OpenPax.Sim.Players;

namespace OpenPax.Ui.Widgets;

/// <summary>
/// A spy mission of the player on a black line: its kind, how much is paid
/// and the target. Redrawn every frame, so the percent moves.
/// </summary>
public sealed class MissionRow(SpyMission mission) : TextRow
{
    public SpyMission Mission { get; } = mission;

    /// <summary>"Steal Ship 40% vs. Name": the kind (STR# 15100), the share paid, the target.</summary>
    public static string Describe(UiAssets assets, SpyMission m)
    {
        var s = assets.Strings;
        return s.Get(new TextRef(15100, m.Kind)) + " " + Math.Min(m.Percent, 100).ToString(System.Globalization.CultureInfo.InvariantCulture)
            + s.Get(new TextRef(104, 4)) + s.Get(new TextRef(80, 10)) + m.Target.Name;
    }

    public override string Text(UiAssets assets) => Describe(assets, Mission);

    public override void Draw(Framebuffer target, UiAssets assets, int x, int y, int width, bool selected)
    {
        target.FillRect(x, y, width, Height, 0);
        var font = assets.Fonts.ForIndex(1);
        font.Draw(target, Text(assets), x + 1, y + 10, 0, 0, selected ? (byte)255 : (byte)9, 0);
    }
}
