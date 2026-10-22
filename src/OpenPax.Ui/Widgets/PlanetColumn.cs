using System.Globalization;
using OpenPax.Data.Views;
using OpenPax.Render;
using OpenPax.Sim.Galaxy;
using OpenPax.Sim.Numerics;
using OpenPax.Sim.Players;

namespace OpenPax.Ui.Widgets;

/// <summary>
/// A planet in the Domestic Policy window's browser, a column
/// 88 px wide: the planet turning when picked, its name, habitability,
/// wealth and population, and for a colony in view its owner, flag, income,
/// espionage, research, construction, subsidy and plan, with a shipyard
/// mark and a defense bar. Rows line up with the header captions beside it.
/// </summary>
public sealed class PlanetColumn(Planet planet, Star star, Player viewer) : ListRow
{
    public const int SortByEmpire = 100;
    public const int SortByName = 101;
    public const int SortByHabitability = 102;
    public const int SortByWealth = 103;
    public const int SortByPopulation = 104;
    public const int SortByIncome = 105;
    public const int SortByEspionage = 106;
    public const int SortByResearch = 107;
    public const int SortByConstruction = 108;
    public const int SortBySubsidy = 109;
    public const int SortByPlan = 110;

    private static readonly CultureInfo _invariant = CultureInfo.InvariantCulture;

    // Defense structures summed for the bar: missile base, fighter base, battle station, mine field.
    private static readonly int[] _defenseKinds = [7, 9, 8, 10];

    private int _frame;

    public Planet Planet { get; } = planet;

    public Star Star { get; } = star;

    public override int Height => 250;

    public override int Width => 88;

    /// <summary>The plan names (STR# 103), set by the window so the sort can compare them.</summary>
    public IReadOnlyList<string> PlanNames { get; init; } = [];

    /// <summary>The colony as the viewer sees it: none unless the star is in view.</summary>
    private Colony? Seen => Star.Sight[viewer.Index] == Star.InView ? Planet.Colony : null;

    /// <summary>A 66 ms step: the picture turns when picked, and the battle mark when in battle.</summary>
    public void Advance(bool selected)
    {
        if (selected || Planet.InCombat)
        {
            _frame++;
            if (_frame > 19)
            {
                _frame = 0;
            }
        }
        else
        {
            _frame = 0;
        }
    }

    public override void Draw(Framebuffer target, UiAssets assets, int x, int y, int width, bool selected)
    {
        var colony = Seen;
        var level = colony is null ? Sim.Players.Treaty.None
            : colony.Owner == viewer ? 4 : viewer.Treaties[colony.Owner.Index].Level();
        var color = level switch
        {
            Sim.Players.Treaty.None => selected ? (byte)255 : (byte)5,
            < 1 => selected ? (byte)18 : (byte)40,
            <= 3 => selected ? (byte)16 : (byte)92,
            _ => selected ? (byte)14 : (byte)22,
        };

        var main = assets.Pictures(ImageSet.Main);
        if (main.Get(selected ? 17008 : 17013) is { } back)
        {
            target.Draw(back, x, y);
        }

        var cx = x + 44;
        var picture = SystemView.PlanetBase(Planet) + 20 + (selected ? _frame : 0);
        MapPane.DrawCentered(target, assets.Pictures(ImageSet.Planets).Get(picture), cx + 12, y + 23);
        if (Planet.InCombat)
        {
            MapPane.DrawCentered(target, main.Get(20300 + (_frame % 25)), cx + 12, y + 23);
        }

        var font = assets.Fonts.ForIndex(1);
        void Text(string text, int dx, int dy) => font.Draw(target, text, cx + dx, y + dy, 0, 1, color, 0);

        Text(Planet.Name, 0, 80);
        MapPane.DrawCentered(target, main.Get(17001 + Colony.Habitability(viewer.Species, Planet)), cx, y + 93);
        Text(Planet.Score.ToString("0.0", _invariant), 0, 115);

        var strings = assets.Strings;
        if (colony is null)
        {
            // What the viewer could grow there.
            Text(Number(Colony.MaximumPopulation(viewer, Planet, null)), 0, 133);
            return;
        }

        // Quirk kept: a shrinking colony reads "+-0.4".
        Text(Number(colony.CurrentPopulation) + strings.Get(new TextRef(108, 4)) + Number(colony.MaxPopulation)
            + " +" + (colony.Growth * 10.0).ToString("0.0", _invariant), 0, 133);

        if (assets.Pictures(ImageSet.Empire).Get(colony.Owner.Flag + 1000) is { } flag)
        {
            target.Draw(flag, cx - 35, y + 6);
        }

        Text(colony.Owner.Name, 0, 61);
        Text(strings.Get(new TextRef(104, 0)) + Number(Income(colony)), 0, 151);
        Text(Number(colony.Resources[Colony.Espionage] * 10.0), 0, 169);
        Text(Number(colony.Resources[Colony.ResearchResource] * 10.0), 0, 187);
        Text(Number(colony.Resources[Colony.Construction] * 10.0), 0, 206);
        Text(Number(colony.Funding), 0, 223);

        if (colony.Plan < PlanNames.Count)
        {
            Text(PlanNames[colony.Plan], 1, 241);
            if (colony.FirstOfKind(TechInfo.ShipyardKind) is not null)
            {
                MapPane.DrawCentered(target, main.Get(17010), cx + 36, y + 36);
            }

            var defense = _defenseKinds.Sum(k => colony.FirstOfKind(k)?.Count ?? 0);
            if (defense > 0)
            {
                target.FillRect(cx + 34, y + 26 - defense, 2, defense, 18);
            }
        }
    }

    private static double Income(Colony c) => c.Resources[Colony.Credits] * 10.0 - Sim.Game.ColonyUpkeep(c);

    private static string Number(double value) => X87.ToInt(value).ToString("#,0", _invariant);

    /// <summary>
    /// Name, habitability and wealth for all; population puts colonies first;
    /// the colony figures put the viewer's own colonies first, then compare
    /// them largest first (the plan by name). Ties keep their order.
    /// </summary>
    public override int Compare(ListRow other, int sortCode)
    {
        if (other is not PlanetColumn row)
        {
            return 0;
        }

        var (a, b) = (Planet, row.Planet);
        switch (sortCode)
        {
            case SortByName:
                return string.CompareOrdinal(a.Name, b.Name);
            case SortByHabitability:
                return Colony.Habitability(viewer.Species, b).CompareTo(Colony.Habitability(viewer.Species, a));
            case SortByWealth:
                return b.Score.CompareTo(a.Score);
            case SortByPopulation:
                if ((a.Colony is null) != (b.Colony is null))
                {
                    return a.Colony is null ? 1 : -1;
                }

                return a.Colony is { } ca && b.Colony is { } cb
                    ? cb.CurrentPopulation.CompareTo(ca.CurrentPopulation)
                    : Colony.MaximumPopulation(viewer, b, null).CompareTo(Colony.MaximumPopulation(viewer, a, null));
        }

        // The raw colony, not the one in view (quirk kept).
        if ((a.Colony is null) != (b.Colony is null))
        {
            return a.Colony is null ? 1 : -1;
        }

        if (a.Colony is not { } x || b.Colony is not { } y)
        {
            return 0;
        }

        if ((x.Owner == viewer) != (y.Owner == viewer))
        {
            return x.Owner == viewer ? -1 : 1;
        }

        if (x.Owner != viewer)
        {
            return 0;
        }

        return sortCode switch
        {
            SortByEmpire => string.CompareOrdinal(x.Owner.Name, y.Owner.Name),
            SortByIncome => Income(y).CompareTo(Income(x)),
            SortByEspionage => y.Resources[Colony.Espionage].CompareTo(x.Resources[Colony.Espionage]),
            SortByResearch => y.Resources[Colony.ResearchResource].CompareTo(x.Resources[Colony.ResearchResource]),
            SortByConstruction => y.Resources[Colony.Construction].CompareTo(x.Resources[Colony.Construction]),
            SortBySubsidy => y.Funding.CompareTo(x.Funding),
            SortByPlan => string.CompareOrdinal(PlanName(x), PlanName(y)),
            _ => 0,
        };
    }

    private string PlanName(Colony c) => c.Plan < PlanNames.Count ? PlanNames[c.Plan] : "";
}
