using System.Globalization;
using OpenPax.Data.Views;
using OpenPax.Render;
using OpenPax.Sim.Players;

namespace OpenPax.Ui.Widgets;

/// <summary>
/// A ship in the Fleet Control window and the upgrade popup:
/// its condition on a black box, then class and design name.
/// </summary>
public sealed class ShipRow(Design ship) : ListRow
{
    public const int SortByCondition = 500;
    public const int SortByName = 501;

    public Design Ship { get; } = ship;

    public override void Draw(Framebuffer target, UiAssets assets, int x, int y, int width, bool selected)
    {
        target.FillRect(x, y, 50, Height, 0);
        var font = assets.Fonts.ForIndex(1);
        var baseline = y + Height - (font.LineHeight - font.AscentFor(0));
        var color = selected ? (byte)28 : (byte)159;
        var condition = Ship.ConditionPercent().ToString(CultureInfo.InvariantCulture) + assets.Strings.Get(new TextRef(109, 2));
        font.Draw(target, condition, x + 40, baseline, 0, 2, color, 0);
        font.Draw(target, Ship.ClassName + ": " + Ship.Name, x + 70, baseline, 0, 0, color, 0);
    }

    /// <summary>Condition largest first, or design name ascending.</summary>
    public override int Compare(ListRow other, int sortCode)
    {
        if (other is not ShipRow row)
        {
            return 0;
        }

        return sortCode switch
        {
            SortByCondition => row.Ship.ConditionPercent().CompareTo(Ship.ConditionPercent()),
            SortByName => string.CompareOrdinal(Ship.Name, row.Ship.Name),
            _ => 0,
        };
    }
}
