using System.Globalization;
using OpenPax.Data.Tables.Tech;
using OpenPax.Data.Views;
using OpenPax.Render;

namespace OpenPax.Ui.Widgets;

/// <summary>
/// Class 18002, a tech's figures by its class: a caption on the left and
/// the value right aligned, a row each 14 px (STR# 18002 names them).
/// Installations show their description instead; improvements nothing.
/// </summary>
public sealed class TechStatsPane(ViewNode node, int left, int top) : Pane(node, left, top)
{
    private const byte TextColor = 25;

    private static readonly CultureInfo _invariant = CultureInfo.InvariantCulture;

    /// <summary>Shows a hull's description (STR# 400) instead of a tech (id 1000).</summary>
    public const int HullText = 1000;

    /// <summary>Shows an armor class's description (STR# 300) instead of a tech (id 1001).</summary>
    public const int ArmorText = 1001;

    /// <summary>The tech shown, <see cref="HullText"/>, <see cref="ArmorText"/>, or -1 for none.</summary>
    public int TechId { get; set; } = -1;

    /// <summary>The hull or armor class for the two description ids (+0x44).</summary>
    public int Extra { get; set; }

    private string S(int list, int index) => Assets.Strings.Get(new TextRef(list, index));

    private string Caption(int entry) => S(18002, entry);

    /// <summary>A whole number with commas every three digits.</summary>
    private static string Number(int value) => value.ToString("#,0", _invariant);

    private static string Plus(int value) => "+" + Number(value) + "%";

    protected override void Draw(Framebuffer target)
    {
        if (TechId is HullText or ArmorText)
        {
            var list = TechId == HullText ? 400 : 300;
            WrappedText.Draw(target, Assets.Fonts.ForIndex(1), S(list, Extra), Left, Top + 10, Width, Height - 10, 0, 0, TextColor, 0);
            return;
        }

        if (TechId < 0 || Assets.Tech(TechId) is not { } tech)
        {
            return;
        }

        var rows = new List<(int Caption, string Value)>();
        switch (tech.Details)
        {
            // Quirk kept: any class but 2 and 3 reads Carrier.
            case StarDrive d:
                rows.Add((0, Number(d.Rating)));
                rows.Add((1, S(20005, d.MaxHullClass switch { 2 => 8, 3 => 9, _ => 11 })));
                break;

            case TacticalDrive d:
                rows.Add((2, Number(d.CombatSpeed)));
                rows.Add((3, Plus(d.DefenseBonus)));
                break;

            case TargetingSensor d:
                rows.Add((0, Caption(d.Range < 501 ? 36 : d.Range < 751 ? 35 : 34)));
                rows.Add((5, Plus(d.AccuracyBonus)));
                rows.Add((6, Number(d.TargetsTracked)));
                break;

            case Ecm d:
                rows.Add((7, "-" + Number(d.Strength) + "%"));
                break;

            // the furthest band with any damage names the range.
            case ShipWeapon d:
                rows.Add((26, Caption(d.WeaponType switch { 1 => 31, 0 => 30, _ => 32 })));
                rows.Add((9, Caption(d.DamageByRange[3] >= 1 ? 33 : d.DamageByRange[2] >= 1 ? 34 : d.DamageByRange[1] >= 1 ? 35 : 36)));
                rows.Add((10, Number(d.DamageByRange[0])));
                rows.Add((11, Plus(d.Accuracy)));
                rows.Add((27, Caption(d.Reload >= 85 ? 37 : d.Reload >= 65 ? 38 : d.Reload >= 25 ? 39 : d.Reload >= 5 ? 40 : 41)));
                rows.Add((28, Caption(d.Tracking == 0xFF ? 43 : 42)));
                break;

            case Shield d:
                rows.Add((44, Number(d.Percentereduction) + "%"));
                rows.Add((15, Number(d.Capacity)));
                rows.Add((18, Number(d.Regeneration)));
                rows.Add((17, Caption(d.IsField == 0 ? 43 : 42)));
                break;

            // the weapons by name under the figures.
            case Fighter d:
                rows.Add((19, Number(d.Speed)));
                rows.Add((22, Number(d.Defense)));
                rows.Add((23, Number(d.HitPoints)));
                rows.Add((20, ""));
                DrawRows(target, rows);
                var small = Assets.Fonts.ForIndex(6);
                if (d.Weapon1 > 0)
                {
                    small.Draw(target, S(100000 + d.Weapon1, 0), Left + 10, Top + 66, 0, 0, TextColor, 0);
                }

                if (d.Weapon2 > 0)
                {
                    small.Draw(target, S(100000 + d.Weapon2, 0), Left + 10, Top + 80, 0, 0, TextColor, 0);
                }

                return;

            case Armor d:
                rows.Add((24, Number(d.MaxHullClass)));
                rows.Add((25, d.StructureMultiplier.ToString("0.0", _invariant)));
                break;

            case Installation:
                WrappedText.Draw(target, Assets.Fonts.ForIndex(1), S(100000 + TechId, 1), Left, Top + 10, Width, Height - 10, 0, 1, TextColor, 0);
                return;
        }

        DrawRows(target, rows);
    }

    private void DrawRows(Framebuffer target, List<(int Caption, string Value)> rows)
    {
        var font = Assets.Fonts.ForIndex(1);
        for (var r = 0; r < rows.Count; r++)
        {
            var baseline = Top + 10 + (14 * r);
            font.Draw(target, Caption(rows[r].Caption), Left + 2, baseline, 0, 0, TextColor, 0);
            font.Draw(target, rows[r].Value, Left + Width, baseline, 0, 2, TextColor, 0);
        }
    }
}
