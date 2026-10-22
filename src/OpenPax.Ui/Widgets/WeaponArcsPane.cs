using System.Globalization;
using OpenPax.Data.Views;
using OpenPax.Render;
using OpenPax.Sim.Players;

namespace OpenPax.Ui.Widgets;

/// <summary>
/// Class 19500, the design seen from above with its firing arcs: for each
/// arc (bow, port, starboard, point defense) and each of the first three
/// range bands a wedge where it has firepower, with the damage written in.
/// The arc being edited is outlined.
/// </summary>
public sealed class WeaponArcsPane(ViewNode node, int left, int top) : Pane(node, left, top)
{
    // Per arc, the wedge pictures for bands 0 to 2 and their offsets.
    private static readonly (int Picture, int X, int Y)[][] _wedges =
    [
        [(26510, 76, 51), (26511, 65, 28), (26512, 51, 1)],
        [(26520, 51, 58), (26521, 27, 37), (26522, 1, 15)],
        [(26530, 98, 58), (26531, 98, 38), (26532, 98, 15)],
        [(26540, 50, 50)],
    ];

    private static readonly (int Picture, int X, int Y)[] _outlines = [(26513, 50, 2), (26523, 1, 14), (26533, 97, 14), (26543, 49, 50)];

    public Design? Design { get; set; }

    /// <summary>The arc outlined, or -1 (+0x44).</summary>
    public int Highlighted { get; set; } = -1;

    protected override void Draw(Framebuffer target)
    {
        var main = Assets.Pictures(ImageSet.Main);
        if (main.Get(26500) is { } back)
        {
            target.Draw(back, Left, Top);
        }

        if (Design is not { } design)
        {
            return;
        }

        for (var mode = 0; mode < _wedges.Length; mode++)
        {
            for (var band = 0; band < _wedges[mode].Length; band++)
            {
                var (picture, x, y) = _wedges[mode][band];
                if (design.Firepower(mode, band) > 0 && main.Get(picture) is { } wedge)
                {
                    target.Draw(wedge, Left + x, Top + y);
                }
            }
        }

        if (Highlighted is >= 0 and < 4 && main.Get(_outlines[Highlighted].Picture) is { } outline)
        {
            target.Draw(outline, Left + _outlines[Highlighted].X, Top + _outlines[Highlighted].Y);
        }

        var color = design.Owner?.FleetColor ?? 0;
        MapPane.DrawCentered(target, main.Get(design.Type + 26300 + (color * 6)), Left + 96, Top + 96);

        // Band 3 is never written.
        var font = Assets.Fonts.ForIndex(1);
        void Number(int mode, int band, int x, int y, int align)
        {
            var value = design.Firepower(mode, band);
            if (value > 0)
            {
                font.Draw(target, value.ToString("#,0", CultureInfo.InvariantCulture), Left + x, Top + y, 0, align, 255, 0);
            }
        }

        Number(0, 0, 96, 63, 1);
        Number(0, 1, 96, 39, 1);
        Number(0, 2, 96, 13, 1);
        Number(3, 0, 96, 141, 1);
        Number(1, 0, 50, 102, 0);
        Number(1, 1, 28, 102, 0);
        Number(1, 2, 3, 102, 0);
        Number(2, 0, 139, 102, 2);
        Number(2, 1, 165, 102, 2);
        Number(2, 2, 190, 102, 2);
    }
}
