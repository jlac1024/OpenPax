using System.Globalization;
using OpenPax.Data.Views;
using OpenPax.Render;
using OpenPax.Sim.Numerics;
using OpenPax.Sim.Players;

namespace OpenPax.Ui.Widgets;

/// <summary>
/// A line of a colony's work queue on the Planetary Surface:
/// an improvement and how far it is built, the ship at the head of the
/// shipyard and its progress, or a structure being repaired and its health.
/// </summary>
public sealed class WorkRow : ListRow
{
    // STR# 101 entries naming each structure kind, by kind.
    private static readonly Dictionary<int, int> _kindNames = new()
    {
        [TechInfo.ShipyardKind] = 0,
        [TechInfo.TrainingBaseKind] = 1,
        [TechInfo.FighterBaseKind] = 2,
        [TechInfo.MissileBaseKind] = 3,
        [TechInfo.BattleStationKind] = 4,
        [TechInfo.MineFieldKind] = 5,
    };

    private WorkRow(Improvement? improvement, Structure? yard, Structure? repair)
    {
        Improvement = improvement;
        Yard = yard;
        Repair = repair;
    }

    public Improvement? Improvement { get; }

    public Structure? Yard { get; }

    public Structure? Repair { get; }

    public static WorkRow Building(Improvement improvement) => new(improvement, null, null);

    public static WorkRow Shipbuilding(Structure yard) => new(null, yard, null);

    public static WorkRow Repairing(Structure structure) => new(null, null, structure);

    public override void Draw(Framebuffer target, UiAssets assets, int x, int y, int width, bool selected)
    {
        var s = assets.Strings;
        var invariant = CultureInfo.InvariantCulture;
        string text;
        int figure;
        if (Improvement is { } improvement)
        {
            text = s.Get(new TextRef(100000 + improvement.Tech.Id, 0));

            // Quirk kept: truncated and kept to a byte.
            figure = (int)improvement.Percent & 0xFF;
        }
        else if (Yard is { Queue.Count: > 0 } yard)
        {
            text = yard.Queue[0].ClassName + ": " + yard.Queue[0].Name;
            figure = X87.ToInt(yard.Progress / yard.Total * 100.0);
        }
        else if (Repair is { } repair)
        {
            text = s.Get(new TextRef(101, 6)) + repair.Count.ToString(invariant) + " "
                + s.Get(new TextRef(101, _kindNames.GetValueOrDefault(repair.Tech.Kind)));
            figure = X87.ToInt(repair.Percent);
        }
        else
        {
            return;
        }

        var font = assets.Fonts.ForIndex(0);
        var baseline = y + Height - (font.LineHeight - font.AscentFor(0));
        font.Draw(target, text, x + 10, baseline, 0, 0, 0x16, 0);

        // Open: the rows are 640 wide in the original; the port puts the figure at the list's right.
        font.Draw(target, figure.ToString("#,0", invariant) + s.Get(new TextRef(104, 4)), x + width - 5, baseline, 0, 2, 0x16, 0);
    }

    public override int Compare(ListRow other, int sortCode)
    {
        return 0;
    }
}
