using System.Globalization;
using OpenPax.Data.Views;
using OpenPax.Render;
using OpenPax.Sim.Players;

namespace OpenPax.Ui.Widgets;

/// <summary>
/// A tech in the Research window's list: name, research line
/// (STR# 105) and the cost for this player, or "Discovered" for a known one.
/// A tech being researched shows in its own color.
/// </summary>
public sealed class TechRow(TechInfo tech, int cost, bool known, bool researching) : ListRow
{
    public const int SortByName = 400;
    public const int SortByType = 401;
    public const int SortByCost = 402;

    private string _name = "";
    private string _group = "";

    public TechInfo Tech { get; } = tech;

    public int Cost { get; } = cost;

    public bool Known { get; } = known;

    public bool Researching { get; } = researching;

    public override int Height => 18;

    /// <summary>Reads the row's words once, so sorting can compare them.</summary>
    public TechRow Named(UiAssets assets)
    {
        _name = assets.Strings.Get(new TextRef(100000 + Tech.Id, 0));
        _group = assets.Strings.Get(new TextRef(105, Tech.ResearchGroup));
        return this;
    }

    public override void Draw(Framebuffer target, UiAssets assets, int x, int y, int width, bool selected)
    {
        var font = assets.Fonts.ForIndex(1);
        var color = selected ? (Researching ? (byte)16 : (byte)14) : Researching ? (byte)55 : (byte)26;
        var baseline = y + Height - (font.LineHeight - font.AscentFor(0));
        font.Draw(target, _name, x + 3, baseline, 0, 0, color, 0);
        // The type and cost columns keep their place from the right (Grown).
        font.Draw(target, _group, x + 282 + Grown, baseline, 0, 2, color, 0);
        var cost = Known ? assets.Strings.Get(new TextRef(108, 1)) : Cost.ToString(CultureInfo.InvariantCulture);
        font.Draw(target, cost, x + 363 + Grown, baseline, 0, 2, color, 0);
    }

    /// <summary>By name, research line or cost; ties under line and cost fall back to the name.</summary>
    public override int Compare(ListRow other, int sortCode)
    {
        if (other is not TechRow row)
        {
            return 0;
        }

        var byName = string.CompareOrdinal(_name, row._name);
        var result = sortCode switch
        {
            SortByType => string.CompareOrdinal(_group, row._group),
            SortByCost => Cost.CompareTo(row.Cost),
            _ => byName,
        };
        return result != 0 ? result : byName;
    }
}
