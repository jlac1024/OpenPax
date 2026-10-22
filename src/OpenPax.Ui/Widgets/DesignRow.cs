using OpenPax.Render;
using OpenPax.Sim.Players;

namespace OpenPax.Ui.Widgets;

/// <summary>
/// A design in the build list: its class name, then its name,
/// color 28 when selected and 159 otherwise.
/// </summary>
public sealed class DesignRow(Design design) : ListRow
{
    public const int SortByType = 502;
    public const int SortByName = 503;

    public Design Design { get; } = design;

    public override void Draw(Framebuffer target, UiAssets assets, int x, int y, int width, bool selected)
    {
        var font = assets.Fonts.ForIndex(1);
        var baseline = y + Height - (font.LineHeight - font.AscentFor(0));
        var color = selected ? (byte)28 : (byte)159;
        font.Draw(target, Design.ClassName, x + 5, baseline, 0, 0, color, 0);
        font.Draw(target, Design.Name, x + 40, baseline, 0, 0, color, 0);
    }

    /// <summary>Class names or design names, byte by byte, case sensitive.</summary>
    public override int Compare(ListRow other, int sortCode)
    {
        if (other is not DesignRow row)
        {
            return 0;
        }

        return sortCode == SortByName
            ? string.CompareOrdinal(Design.Name, row.Design.Name)
            : string.CompareOrdinal(Design.ClassName, row.Design.ClassName);
    }
}
