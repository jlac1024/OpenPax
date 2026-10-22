using OpenPax.Data.Views;
using OpenPax.Render;
using OpenPax.Sim.Players;

namespace OpenPax.Ui.Widgets;

/// <summary>
/// Class 13002 on the Sector Display: the selected fleet's ships, one 42 by
/// 42 cell each, framed by condition, with name and experience. A click on
/// an own fleet's ship unselects it or selects it again (the last selected
/// ship cannot be unselected); unselected ships are split off into a fleet
/// of their own before the fleet's next order.
/// </summary>
public sealed class FleetPanelPane(ViewNode node, int left, int top) : Pane(node, left, top)
{
    private const int Cell = 42;

    private Fleet? _fleet;

    public GameSession? Session { get; set; }

    /// <summary>The fleet shown; showing a fleet selects all its ships again.</summary>
    public Fleet? Fleet
    {
        get => _fleet;
        set
        {
            _fleet = value;
            if (Session is { } session)
            {
                session.LeftOut.Clear();
                session.LeftOutFleet = value;
            }
        }
    }

    // up to 21, grouped by hull, the biggest first.
    private static List<Design> Ships(Fleet fleet) => fleet.Ships.OrderByDescending(s => s.Type).Take(Fleet.MaxShips).ToList();

    private bool Selected(Design ship) => Session is null || Session.LeftOutFleet != _fleet || !Session.LeftOut.Contains(ship.ShipId);

    protected internal override void MouseDown(int x, int y, int clicks)
    {
        if (_fleet is not { } fleet || Session is not { } session || fleet.Owner != session.Local)
        {
            return;
        }

        var columns = Math.Max(1, Width / Cell);
        var i = ((y - Top) / Cell * columns) + ((x - Left) / Cell);
        var ships = Ships(fleet);
        if (x - Left >= columns * Cell || i < 0 || i >= ships.Count)
        {
            return;
        }

        var id = ships[i].ShipId;
        if (session.LeftOut.Contains(id))
        {
            session.LeftOut.Remove(id);
        }
        else if (fleet.Ships.Count(s => !session.LeftOut.Contains(s.ShipId)) > 1)
        {
            session.LeftOut.Add(id);
        }
    }

    protected override void Draw(Framebuffer target)
    {
        if (Fleet is not { } fleet)
        {
            return;
        }

        var ships = Ships(fleet);
        var columns = Math.Max(1, Width / Cell);
        var infra = Assets.Pictures(ImageSet.Infra);
        var font = Assets.Fonts.ForIndex(6);
        for (var i = 0; i < ships.Count; i++)
        {
            var ship = ships[i];
            var x = Left + (i % columns * Cell);
            var y = Top + (i / columns * Cell);
            if (infra.Get((fleet.Owner.FleetColor + 1) * 50) is { } sprite)
            {
                target.Draw(sprite, x + 2, y + 24 - sprite.Height / 2);
            }

            var condition = ship.ConditionPercent();
            Frame(target, x + 1, y + 1, Cell - 2, Cell - 2, condition < 34 ? (byte)0x12 : condition < 67 ? (byte)0x10 : (byte)0x16);
            var selected = Selected(ship);
            font.Draw(target, ship.Name, x + 3, y + 12, 0, 0, selected ? (byte)0xFF : (byte)2, 0);
            if (infra.Get(ship.ExperienceLevel + (selected ? 10 : 30)) is { } rank)
            {
                target.Draw(rank, x + 40 - rank.Width, y + 24 - rank.Height / 2);
            }
        }
    }

    private static void Frame(Framebuffer target, int x, int y, int width, int height, byte color)
    {
        target.FillRect(x, y, width, 1, color);
        target.FillRect(x, y + height - 1, width, 1, color);
        target.FillRect(x, y, 1, height, color);
        target.FillRect(x + width - 1, y, 1, height, color);
    }
}
