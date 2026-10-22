using System.Globalization;
using OpenPax.Data.Views;
using OpenPax.Render;
using OpenPax.Sim.Players;

namespace OpenPax.Ui.Widgets;

/// <summary>
/// Class 8038, a design or ship turning in its owner's colors with its
/// figures. Three layouts: a ship (mode 0, with its crew's experience), a
/// design (mode 1, with cost and upkeep), both in the Fleet Control window,
/// and the Sector Display's build panel (mode 2).
/// </summary>
// mode, draw, picture, text  and
public sealed class DesignPreviewPane(ViewNode node, int left, int top) : Pane(node, left, top)
{
    private const int FrameTime = 66;
    private const byte TextColor = 0x45;

    private static readonly CultureInfo _invariant = CultureInfo.InvariantCulture;

    private long _now;

    public Design? Design { get; set; }

    /// <summary>0 a ship, 1 a design, 2 the build panel's layout.</summary>
    public int Mode { get; set; } = 2;

    /// <summary>The game's tech table, which the strength figure needs.</summary>
    public Func<int, TechInfo>? TechById { get; set; }

    protected internal override void Tick(long now)
    {
        _now = now;
    }

    protected override void Draw(Framebuffer target)
    {
        if (Design is not { Owner: { } owner } design || TechById is not { } techById)
        {
            return;
        }

        var frame = (int)(_now / FrameTime % 32);
        var picture = ((design.Type + ((owner.FleetColor + 1) * 10)) * 100) + frame + 1;
        if (Mode != 2)
        {
            MapPane.DrawCentered(target, Assets.Pictures(ImageSet.Combat).Get(picture), Left + (Width / 2) + 43, Top + (Height / 2) - 20);
            DrawFleetLayout(target, design, owner, techById);
            return;
        }

        MapPane.DrawCentered(target, Assets.Pictures(ImageSet.Combat).Get(picture), Left + (Width / 2), Top + (Height / 2) + 70);

        var (l, t) = (Left, Top);
        Assets.Fonts.ForIndex(2).Draw(target, design.ClassName + ": " + design.Name, l + 73, t + 14, 1, 1, TextColor, 0);

        var colonizes = design.Specials.Any(c => c.Tech.Kind == TechInfo.ColonizerKind);
        (int Label, string Value, int Y)[] rows =
        [
            (0, design.Structure.ToString(_invariant) + Str(108, 4) + design.MaxStructure.ToString(_invariant), 32),
            (1, Str(21000, colonizes ? 2 : 3), 48),
            (9, design.DriveRange().ToString(_invariant), 64),
            (4, design.CombatSpeed().ToString("0.##", _invariant), 80),
            (13, design.Strength(techById).ToString(_invariant), 96),
        ];
        var font = Assets.Fonts.ForIndex(1);
        foreach (var (label, value, y) in rows)
        {
            font.Draw(target, Str(21000, label), l + 18, t + y, 0, 0, TextColor, 0);
            font.Draw(target, value, l + 87, t + y, 0, 0, TextColor, 0);
        }

        // Upkeep is the design's cost, less a fifth for nomads.
        var upkeep = owner.Species.Is(Species.Nomad) ? design.Cost * 0.8 : design.Cost;
        font.Draw(target, Str(21000, 10), l + 12, t + 114, 1, 0, TextColor, 0);
        font.Draw(target, design.BuildCost().ToString(_invariant), l + 97, t + 114, 1, 0, TextColor, 0);
        font.Draw(target, Str(21000, 11), l + 12, t + 130, 1, 0, TextColor, 0);
        font.Draw(target, ((int)upkeep).ToString(_invariant), l + 97, t + 130, 1, 0, TextColor, 0);
    }

    /// <summary>Figures in a column with values 70 px in, the name centered below, and for a design its cost and upkeep.</summary>
    private void DrawFleetLayout(Framebuffer target, Design design, Player owner, Func<int, TechInfo> techById)
    {
        var (l, t) = (Left, Top);
        var colonizes = Str(21000, design.Specials.Any(c => c.Tech.Kind == TechInfo.ColonizerKind) ? 2 : 3);
        var hitPoints = design.Structure.ToString(_invariant) + Str(108, 4) + design.MaxStructure.ToString(_invariant);
        var speed = design.CombatSpeed().ToString("0.0", _invariant);
        var strength = design.Strength(techById).ToString(_invariant);
        (string Label, string Value)[] rows = Mode == 0
            ? [(Str(21000, 0), hitPoints), (Str(20002, 7), Str(20002, design.ExperienceLevel)), (Str(21000, 1), colonizes), (Str(21000, 4), speed), (Str(21000, 13), strength)]
            : [(Str(21000, 0), hitPoints), (Str(21000, 1), colonizes), (Str(21000, 9), design.DriveRange().ToString(_invariant)), (Str(21000, 4), speed), (Str(21000, 13), strength)];

        var font = Assets.Fonts.ForIndex(1);
        for (var i = 0; i < rows.Length; i++)
        {
            font.Draw(target, rows[i].Label, l, t + 10 + (14 * i), 0, 0, TextColor, 0);
            font.Draw(target, rows[i].Value, l + 70, t + 10 + (14 * i), 0, 0, TextColor, 0);
        }

        Assets.Fonts.ForIndex(2).Draw(target, design.ClassName + ": " + design.Name, (l + l + Width) / 2, t + 121, 1, 1, TextColor, 0);
        if (Mode == 1)
        {
            var upkeep = owner.Species.Is(Species.Nomad) ? design.Cost * 0.8 : design.Cost;
            font.Draw(target, Str(21000, 10), l + 35, t + 149, 1, 0, TextColor, 0);
            font.Draw(target, design.BuildCost().ToString(_invariant), l + 122, t + 149, 1, 0, TextColor, 0);
            font.Draw(target, Str(21000, 11), l + 35, t + 165, 1, 0, TextColor, 0);
            font.Draw(target, ((int)upkeep).ToString(_invariant), l + 122, t + 165, 1, 0, TextColor, 0);
        }
    }

    private string Str(int list, int index)
    {
        return Assets.Strings.Get(new TextRef(list, index));
    }
}
