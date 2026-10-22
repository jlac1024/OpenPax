using System.Globalization;
using OpenPax.Data.Tables.Tech;
using OpenPax.Data.Views;
using OpenPax.Render;

namespace OpenPax.Ui.Widgets;

/// <summary>
/// Class 26002, the system fitted in a single slot of the design being
/// made: "No System Installed", or the tech's name and up to three of its
/// figures (as the tech stats show them).
/// </summary>
// draw, class blocks  to
public sealed class SlotPanelPane(ViewNode node, int left, int top) : Pane(node, left, top)
{
    private static readonly CultureInfo _invariant = CultureInfo.InvariantCulture;

    /// <summary>The fitted tech's id, or 0 for none (+0x40).</summary>
    public int TechId { get; set; }

    private string S(int list, int entry) => Assets.Strings.Get(new TextRef(list, entry));

    private static string Number(int value) => value.ToString("#,0", _invariant);

    protected override void Draw(Framebuffer target)
    {
        var font = Assets.Fonts.ForIndex(3);
        var middle = Left + (Width / 2);
        if (TechId <= 0 || Assets.Tech(TechId) is not { } tech)
        {
            font.Draw(target, S(75, 5), middle, Top + 50, 1, 1, 18, 0);
            return;
        }

        font.Draw(target, S(100000 + TechId, 0), middle, Top + 30, 1, 1, 14, 0);
        (int Caption, string Value)[] rows = tech.Details switch
        {
            StarDrive d => [(0, Number(d.Rating)), (1, S(20005, d.MaxHullClass switch { 2 => 8, 3 => 9, _ => 11 }))],
            TacticalDrive d => [(2, Number(d.CombatSpeed)), (3, "+" + Number(d.DefenseBonus) + "%")],
            TargetingSensor d => [(0, S(18002, d.Range < 501 ? 36 : d.Range < 751 ? 35 : 34)), (5, "+" + Number(d.AccuracyBonus) + "%"), (6, Number(d.TargetsTracked))],
            Ecm d => [(7, "-" + Number(d.Strength) + "%")],
            Shield d => [(44, Number(d.Percentereduction) + "%"), (15, Number(d.Capacity)), (18, Number(d.Regeneration))],
            _ => [],
        };

        // Open: the blocks' x positions were not read; captions left, values right, 20 px in.
        for (var r = 0; r < rows.Length; r++)
        {
            var baseline = Top + 60 + (22 * r);
            font.Draw(target, S(18002, rows[r].Caption), Left + 20, baseline, 0, 0, 25, 0);
            font.Draw(target, rows[r].Value, Left + Width - 20, baseline, 0, 2, 25, 0);
        }
    }
}
