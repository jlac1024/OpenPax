using System.Globalization;
using OpenPax.Data.Views;
using OpenPax.Render;
using OpenPax.Sim.Players;

namespace OpenPax.Ui.Widgets;

/// <summary>
/// A shipyard in the Fleet Control list: the planet's name,
/// the yard's condition, its build points, what it builds and how far.
/// </summary>
public sealed class YardRow(Colony colony, Structure yard) : ListRow
{
    public const int SortByName = 700;
    public const int SortByCondition = 701;
    public const int SortByPoints = 702;
    public const int SortByProject = 703;
    public const int SortByProgress = 704;

    private static readonly CultureInfo _invariant = CultureInfo.InvariantCulture;

    public Colony Colony { get; } = colony;

    public Structure Yard { get; } = yard;

    public override int Height => 19;

    private int Condition => (int)Yard.Percent;

    // on a tenth of the period's construction.
    private int Points => (int)Sim.Game.YardWork(Yard, Colony.Resources[Colony.Construction] * 10.0);

    private int Progress => (int)(Yard.Progress / Yard.Total * 100.0);

    public override void Draw(Framebuffer target, UiAssets assets, int x, int y, int width, bool selected)
    {
        if (assets.Pictures(ImageSet.Main).Get(20021) is { } bar)
        {
            // Placed by its top right at the row's right edge less one.
            target.Draw(bar, x + 373 + Grown - 1 - bar.Width, y - 1);
        }

        var font = assets.Fonts.ForIndex(1);
        var baseline = y + Height - (font.LineHeight - font.AscentFor(0)) - 2;
        var color = selected ? (byte)0x0E : (byte)0x16;
        var strings = assets.Strings;
        var percent = strings.Get(new TextRef(109, 2));
        var head = Yard.Queue.Count > 0 ? Yard.Queue[0] : null;

        font.Draw(target, Colony.Planet.Name, x + 28, baseline, 0, 0, color, 0);
        font.Draw(target, Condition.ToString(_invariant) + percent, x + 170 + Grown, baseline, 0, 2, color, 0);
        font.Draw(target, Points.ToString("#,0", _invariant), x + 213 + Grown, baseline, 0, 2, color, 0);
        var project = head is null ? strings.Get(new TextRef(85, 5))
            : Yard.Queue.Count.ToString(_invariant) + " / " + head.ClassName + ": " + head.Name;
        font.Draw(target, project, x + 328 + Grown, baseline, 0, 2, color, 0);
        font.Draw(target, head is null ? "----" : Progress.ToString(_invariant) + percent, x + 370 + Grown, baseline, 0, 2, color, 0);
    }

    /// <summary>Name ascending; condition, points and progress largest first; busy yards first, then by the ship's design name.</summary>
    public override int Compare(ListRow other, int sortCode)
    {
        if (other is not YardRow row)
        {
            return 0;
        }

        switch (sortCode)
        {
            case SortByName:
                return string.CompareOrdinal(Colony.Planet.Name, row.Colony.Planet.Name);
            case SortByCondition:
                return row.Condition.CompareTo(Condition);
            case SortByPoints:
                return row.Points.CompareTo(Points);
            case SortByProject:
                var (a, b) = (Yard.Queue.Count > 0 ? Yard.Queue[0] : null, row.Yard.Queue.Count > 0 ? row.Yard.Queue[0] : null);
                return a is null || b is null ? (b is null ? 0 : 1) - (a is null ? 0 : 1) : string.CompareOrdinal(a.Name, b.Name);
            case SortByProgress:
                return row.ProgressOrZero.CompareTo(ProgressOrZero);
            default:
                return 0;
        }
    }

    private int ProgressOrZero => Yard.Queue.Count > 0 ? Progress : 0;
}
