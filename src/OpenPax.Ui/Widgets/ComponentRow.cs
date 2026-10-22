using System.Globalization;
using OpenPax.Data.Views;
using OpenPax.Render;
using OpenPax.Sim.Players;

namespace OpenPax.Ui.Widgets;

/// <summary>
/// A component in the Ship Design lists: name, then its space
/// and build cost for the player, or (in the special systems list) the count
/// fitted. Fitted rows start with the count and show the space
/// and cost of all of them.
/// </summary>
public sealed class ComponentRow : ListRow
{
    public const int SortByName = 200;
    public const int SortBySpace = 201;
    public const int SortByCost = 202;
    public const int SortByCount = 203;

    private readonly string _name;
    private readonly bool _countOnly;
    private readonly byte? _color;
    private readonly byte? _selectedColor;

    private ComponentRow(TechInfo tech, string name, int count, int size, int cost, bool countOnly, Component? fitted, byte? color, byte? selectedColor)
    {
        Tech = tech;
        _name = name;
        Count = count;
        Size = size;
        Cost = cost;
        _countOnly = countOnly;
        Fitted = fitted;
        _color = color;
        _selectedColor = selectedColor;
    }

    public TechInfo Tech { get; }

    public int Count { get; }

    public int Size { get; }

    public int Cost { get; }

    /// <summary>The design's entry this row shows, for a fitted row.</summary>
    public Component? Fitted { get; }

    public override int Height => 15;

    private static string Name(UiAssets assets, TechInfo tech) => assets.Strings.Get(new TextRef(100000 + tech.Id, 0));

    /// <summary>A tech that can be fitted, with its space and build cost for the player.</summary>
    public static ComponentRow Available(UiAssets assets, TechInfo tech, Player player)
    {
        return new(tech, Name(assets, tech), 1, TechLevels.Size(tech, player), TechLevels.BuildCost(tech, player), false, null, null, null);
    }

    /// <summary>A special system fitted, with its count (colors 25, picked 16).</summary>
    public static ComponentRow Special(UiAssets assets, Component c, Player? player)
    {
        return new(c.Tech, Name(assets, c.Tech), c.Count, TechLevels.Size(c.Tech, player), TechLevels.BuildCost(c.Tech, player), true, c, 25, 16);
    }

    /// <summary>A fitted entry with its count; space and cost are of all of them.</summary>
    public static ComponentRow FittedRow(UiAssets assets, Component c, Player? player)
    {
        return new(c.Tech, Name(assets, c.Tech), c.Count, TechLevels.Size(c.Tech, player) * c.Count, TechLevels.BuildCost(c.Tech, player) * c.Count, false, c, null, null);
    }

    public override void Draw(Framebuffer target, UiAssets assets, int x, int y, int width, bool selected)
    {
        var invariant = CultureInfo.InvariantCulture;
        if (Fitted is not null && !_countOnly)
        {
            var font = assets.Fonts.ForIndex(1);
            var baseline = y + Height - (font.LineHeight - font.AscentFor(0));
            var color = selected ? (byte)14 : (byte)25;
            font.Draw(target, Count.ToString(invariant), x + 22, baseline, 0, 1, color, 0);
            font.Draw(target, _name, x + 50, baseline, 0, 0, color, 0);
            font.Draw(target, Size.ToString(invariant), x + 233, baseline, 0, 2, color, 0);
            font.Draw(target, Cost.ToString(invariant), x + 276, baseline, 0, 2, color, 0);
            return;
        }

        var row = assets.Fonts.ForIndex(0);
        var line = y + Height - (row.LineHeight - row.AscentFor(0));
        var c = selected ? _selectedColor ?? 14 : _color ?? 25;
        row.Draw(target, _name, x + 4, line, 0, 0, c, 0);
        if (_countOnly)
        {
            row.Draw(target, Count.ToString(invariant), x + 150, line, 0, 0, c, 0);
            return;
        }

        row.Draw(target, Size.ToString(invariant), x + 175, line, 0, 2, c, 0);
        row.Draw(target, Cost.ToString(invariant), x + 215, line, 0, 2, c, 0);
    }

    /// <summary>Name (then count, largest first, then space), space, cost, or count largest first; ties by name.</summary>
    public override int Compare(ListRow other, int sortCode)
    {
        if (other is not ComponentRow o)
        {
            return 0;
        }

        var byName = string.CompareOrdinal(_name, o._name);
        return sortCode switch
        {
            SortByName => byName != 0 ? byName : o.Count != Count ? o.Count.CompareTo(Count) : Size.CompareTo(o.Size),
            SortBySpace => Size != o.Size ? Size.CompareTo(o.Size) : byName,
            SortByCost => Cost != o.Cost ? Cost.CompareTo(o.Cost) : byName,
            SortByCount => o.Count != Count ? o.Count.CompareTo(Count) : byName,
            _ => 0,
        };
    }
}
