using OpenPax.Data.Views;
using OpenPax.Render;
using OpenPax.Sim.Numerics;
using OpenPax.Sim.Players;

namespace OpenPax.Ui.Widgets;

/// <summary>
/// A governor in the picker: the system it governs, then its
/// skills for domestic policy, research, espionage and fleet, as percents.
/// Rows go in at the front, so the oldest governor ends up on top.
/// </summary>
public sealed class GovernorRow(Advisor governor, string star) : ListRow
{
    // Columns right edges and their skills: domestic 2, research 0, espionage 1, fleet 3.
    private static readonly (int X, int Skill)[] _columns = [(155, 2), (211, 0), (271, 1), (330, 3)];

    public Advisor Governor { get; } = governor;

    public override void Draw(Framebuffer target, UiAssets assets, int x, int y, int width, bool selected)
    {
        if (!Governor.IsGovernor)
        {
            return;
        }

        var font = assets.Fonts.ForIndex(0);
        var color = selected ? (byte)255 : (byte)9;
        var baseline = y + 10;
        font.Draw(target, star + assets.Strings.Get(new TextRef(107, 6)), x + 1, baseline, 0, 0, color, 0);
        foreach (var (column, skill) in _columns)
        {
            font.Draw(target, Percent(assets, Governor.Value(skill)), x + column, baseline, 0, 2, color, 0);
        }
    }

    /// <summary>
    /// A skill as a whole percent with "% " after it. Quirk kept: a negative
    /// value (a turned governor) goes one lower first, as the float to int
    /// conversion does.
    /// </summary>
    public static string Percent(UiAssets assets, double value)
    {
        return X87.ToInt(value * 100.0).ToString("#,0", System.Globalization.CultureInfo.InvariantCulture)
            + assets.Strings.Get(new TextRef(109, 2));
    }

    /// <summary>Every new row sorts first, so the list ends up in reverse.</summary>
    public override int Compare(ListRow other, int sortCode)
    {
        return 1;
    }
}
