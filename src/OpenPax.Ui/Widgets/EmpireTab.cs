using OpenPax.Render;
using OpenPax.Sim.Players;

namespace OpenPax.Ui.Widgets;

/// <summary>
/// Another player's tab in the Foreign Affairs strip: the
/// flag, a cross once the player is out, and a mark for the relation.
/// </summary>
public sealed class EmpireTab(Player player, Player viewer) : ListRow
{
    public Player Player { get; } = player;

    public override int Height => 25;

    public override int Width => 37;

    public override void Draw(Framebuffer target, UiAssets assets, int x, int y, int width, bool selected)
    {
        var main = assets.Pictures(ImageSet.Main);
        if (main.Get(selected ? 15002 : 15001) is { } tab)
        {
            target.Draw(tab, x, y);
        }

        var d = selected ? 1 : 0;
        if (assets.Pictures(ImageSet.Empire).Get(Player.Flag + 1000) is { } flag)
        {
            target.Draw(flag, x + 8 + d, y + 5 + d);
        }

        if (Player.Eliminated && assets.Pictures(ImageSet.Infra).Get(20) is { } cross)
        {
            target.Draw(cross, x + 8 + d, y + 5 + d);
        }

        var level = Player.Eliminated ? Treaty.None : viewer.Treaties[Player.Index].Level();
        int? mark = level switch
        {
            Treaty.None => null,
            < 1 => 15501,
            <= 3 => 15503,
            _ => 15505,
        };
        if (mark is { } id && main.Get(id) is { } sprite)
        {
            target.Draw(sprite, x + 4 + d, y + 3 + d);
        }
    }

    public override int Compare(ListRow other, int sortCode)
    {
        return 0;
    }
}
