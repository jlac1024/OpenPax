using OpenPax.Render;
using OpenPax.Sim.Players;

namespace OpenPax.Ui.Widgets;

/// <summary>A design a ship may be refitted to, in the upgrade popup: class and name.</summary>
public sealed class RefitRow(Design design) : ListRow
{
    public Design Design { get; } = design;

    public override void Draw(Framebuffer target, UiAssets assets, int x, int y, int width, bool selected)
    {
        var font = assets.Fonts.ForIndex(1);
        var baseline = y + Height - (font.LineHeight - font.AscentFor(0));
        font.Draw(target, Design.ClassName + ": " + Design.Name, x + 10, baseline, 0, 0, selected ? (byte)0x16 : (byte)0x19, 0);
    }

    public override int Compare(ListRow other, int sortCode)
    {
        return 0;
    }
}
