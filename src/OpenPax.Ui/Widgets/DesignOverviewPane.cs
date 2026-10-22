using System.Globalization;
using OpenPax.Data.Views;
using OpenPax.Render;
using OpenPax.Sim.Players;

namespace OpenPax.Ui.Widgets;

/// <summary>
/// Class 26001, a design's overview: class of hull, strength and hit
/// points, then the star drive, tactical drive, shield, ECM and targeting
/// with their raw figures. A missing system's caption is red.
/// </summary>
public sealed class DesignOverviewPane(ViewNode node, int left, int top) : Pane(node, left, top)
{
    private const byte CaptionColor = 105;
    private const byte ValueColor = 25;
    private const byte MissingColor = 18;
    private const string Empty = "--";

    private static readonly CultureInfo _invariant = CultureInfo.InvariantCulture;

    public Design? Design { get; set; }

    /// <summary>The game's tech table, which the strength figure needs.</summary>
    public Func<int, TechInfo>? TechById { get; set; }

    private string S(int list, int entry) => Assets.Strings.Get(new TextRef(list, entry));

    protected override void Draw(Framebuffer target)
    {
        var (l, t) = (Left, Top);
        var small = Assets.Fonts.ForIndex(1);
        var large = Assets.Fonts.ForIndex(3);
        large.Draw(target, S(26300, 16), l + 3, t + 13, 1, 0, CaptionColor, 0);
        small.Draw(target, S(26300, 17), l + 3, t + 34, 0, 0, CaptionColor, 0);
        small.Draw(target, S(20000, 35), l + 169, t + 13, 0, 0, CaptionColor, 0);
        small.Draw(target, S(26300, 18), l + 15, t + 49, 0, 0, ValueColor, 0);
        if (Design is not { } d)
        {
            return;
        }

        large.Draw(target, S(26000, d.Type + 27), l + 70, t + 13, 1, 0, ValueColor, 0);
        small.Draw(target, (TechById is { } techs ? d.Strength(techs) : 0).ToString(_invariant), l + 245, t + 13, 0, 0, ValueColor, 0);
        small.Draw(target, d.MaxStructure.ToString(_invariant), l + 70, t + 49, 0, 0, ValueColor, 0);

        void Text(string text, int x, int y) => small.Draw(target, text, l + x, t + y, 0, 0, ValueColor, 0);
        void Caption(int entry, int x, int y, bool filled) => small.Draw(target, S(26300, entry), l + x, t + y, 0, 0, filled ? CaptionColor : MissingColor, 0);
        var percent = S(109, 2);

        // Star drive.
        if (d.StarDrive is { } star)
        {
            Text(S(26300, 20), 15, 86);
            Text(star.Tech.DriveRating.ToString(_invariant), 70, 86);
        }
        else
        {
            Text(Empty, 15, 86);
        }

        Caption(19, 3, 71, d.StarDrive is not null);

        // Tactical drive.
        if (d.TacticalDrive is { } tac)
        {
            Text(S(26300, 22), 15, 123);
            Text(tac.Tech.CombatSpeed.ToString(_invariant), 70, 123);
            Text(S(26300, 23), 15, 138);
            Text("+" + tac.Tech.DefenseBonus.ToString(_invariant) + percent, 70, 138);
        }
        else
        {
            Text(Empty, 15, 123);
            Text(Empty, 15, 138);
        }

        Caption(21, 3, 108, d.TacticalDrive is not null);

        // Shield: an absorbing one names its capacity, else its reduction.
        if (d.Shield is { } shield)
        {
            Text(shield.Tech.ShieldCapacity != 0
                ? S(70, 2) + shield.Tech.ShieldCapacity.ToString(_invariant)
                : S(70, 1) + shield.Tech.ShieldPercent.ToString(_invariant) + percent, 180, 49);
        }
        else
        {
            Text(Empty, 180, 49);
        }

        Caption(25, 169, 34, d.Shield is not null);

        // ECM. Open: the join order is read loosely; "Defense +N% " assumed.
        Text(d.Ecm is { } ecm ? S(26300, 23) + " +" + ecm.Tech.EcmStrength.ToString(_invariant) + percent : Empty, 179, 86);
        Caption(29, 169, 71, d.Ecm is not null);

        // Targeting.
        if (d.Targeting is { } sensor)
        {
            Text(S(26300, 27), 179, 123);
            Text(S(26300, 28), 179, 138);
            Text("+" + sensor.Tech.SensorAccuracy.ToString(_invariant) + percent, 228, 123);
            Text(Tracked(sensor.Tech).ToString(_invariant), 228, 138);
        }
        else
        {
            Text(Empty, 179, 121);
            Text(Empty, 179, 138);
        }

        Caption(26, 169, 108, d.Targeting is not null);
    }

    private int Tracked(TechInfo sensor)
    {
        return Assets.Tech(sensor.Id)?.Details is OpenPax.Data.Tables.Tech.TargetingSensor s ? s.TargetsTracked : 0;
    }
}
