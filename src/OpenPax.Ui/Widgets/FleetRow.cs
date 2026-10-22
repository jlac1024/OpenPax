using System.Globalization;
using OpenPax.Data.Views;
using OpenPax.Render;
using OpenPax.Sim.Players;

namespace OpenPax.Ui.Widgets;

/// <summary>
/// A fleet in the Fleet Control list: an icon box with the
/// fleet's state turning in it (in battle, intercepting, under orders), then
/// name, condition, speed, ships and strength.
/// </summary>
public sealed class FleetRow(Fleet fleet) : ListRow
{
    public const int SortByName = 600;
    public const int SortByCondition = 601;
    public const int SortBySpeed = 602;
    public const int SortByShips = 603;
    public const int SortByStrength = 604;

    private static readonly CultureInfo _invariant = CultureInfo.InvariantCulture;

    private int _frame;

    public Fleet Fleet { get; } = fleet;

    public override int Height => 19;

    /// <summary>
    /// One icon step, every 66 ms (the list idler). One counter serves all
    /// three icons: it counts up, then wraps by the state's frame count.
    /// </summary>
    public void Advance()
    {
        _frame++;
        var last = Fleet.InCombat ? 24 : Fleet.Intercepting ? 29 : Fleet.Orders.Count > 0 ? 14 : int.MaxValue;
        if (_frame > last)
        {
            _frame = 0;
        }
    }

    public override void Draw(Framebuffer target, UiAssets assets, int x, int y, int width, bool selected)
    {
        var pictures = assets.Pictures(ImageSet.Main);
        if (pictures.Get(20020) is { } box)
        {
            target.Draw(box, x, y - 1);
        }

        var (icon, dy) = Fleet.InCombat ? (20300, 2) : Fleet.Intercepting ? (20350, 2) : Fleet.Orders.Count > 0 ? (20330, 3) : (0, 0);
        if (icon != 0 && pictures.Get(icon + _frame) is { } sprite)
        {
            target.Draw(sprite, x + 2, y - dy);
        }

        // Quirk kept: drawn in pft 10 but placed with pft 11's metrics.
        var font = assets.Fonts.ForIndex(1);
        var metrics = assets.Fonts.ForIndex(2);
        var baseline = y + Height - (metrics.LineHeight - metrics.AscentFor(0)) - 2;

        // Only own fleets are listed, so the color is always the own one.
        var color = selected ? (byte)0x0E : (byte)0x16;
        var percent = assets.Strings.Get(new TextRef(109, 2));
        font.Draw(target, Fleet.Name, x + 28, baseline, 0, 0, color, 0);
        font.Draw(target, Sim.Game.FleetCondition(Fleet).ToString(_invariant) + percent, x + 198 + Grown, baseline, 0, 2, color, 0);
        font.Draw(target, Fleet.Speed.ToString("0.0", _invariant), x + 245 + Grown, baseline, 0, 2, color, 0);
        font.Draw(target, Fleet.Ships.Count.ToString("#,0", _invariant), x + 291 + Grown, baseline, 0, 2, color, 0);
        font.Draw(target, Fleet.Strength.ToString("#,0", _invariant), x + 370 + Grown, baseline, 0, 2, color, 0);
    }

    /// <summary>Name ascending, byte by byte; the figures largest first.</summary>
    public override int Compare(ListRow other, int sortCode)
    {
        if (other is not FleetRow row)
        {
            return 0;
        }

        var (a, b) = (Fleet, row.Fleet);
        return sortCode switch
        {
            SortByName => string.CompareOrdinal(a.Name, b.Name),
            SortByCondition => Sim.Game.FleetCondition(b).CompareTo(Sim.Game.FleetCondition(a)),
            SortBySpeed => b.Speed.CompareTo(a.Speed),
            SortByShips => b.Ships.Count.CompareTo(a.Ships.Count),
            SortByStrength => b.Strength.CompareTo(a.Strength),
            _ => 0,
        };
    }
}
