using OpenPax.Data.Views;
using OpenPax.Render;

namespace OpenPax.Ui.Widgets;

/// <summary>
/// Class 10002 in Game Setup: ships of the chosen fleet color over two
/// star fields scrolling left at different speeds.
/// </summary>
public sealed class FleetColorPane(ViewNode node, int left, int top, ByteOptionDetails details) : Pane(node, left, top)
{
    public const int Colors = 16;
    private const int ScrollPeriod = 10;

    // Where the six ships sit, from the panel's corner (table).
    private static readonly (int X, int Y)[] _sixShips = [(50, 40), (75, 125), (50, 200), (140, 70), (140, 170), (225, 125)];

    private long _lastScroll = -1;
    private int _scroll;

    public int Color { get; private set; }

    public void Set(int color)
    {
        Color = ((color % Colors) + Colors) % Colors;
    }

    public void Step(int by)
    {
        Set(Color + by);
    }

    // 3 every 10 ms.
    protected internal override void Tick(long now)
    {
        if (_lastScroll < 0)
        {
            _lastScroll = now;
        }

        while (now - _lastScroll >= ScrollPeriod)
        {
            _lastScroll += ScrollPeriod;
            _scroll += 3;
        }
    }

    protected override void Draw(Framebuffer target)
    {
        var universe = Assets.Pictures(ImageSet.Universe);
        Tile(target, universe.Get(1), _scroll / 4);
        Tile(target, universe.Get(8), _scroll / 2);

        var combat = Assets.Pictures(ImageSet.Combat);
        if (details.Value != 0)
        {
            for (var i = 0; i < _sixShips.Length; i++)
            {
                if (combat.Get(((i + Color * 10) * 100) + 1001) is { } ship)
                {
                    target.Draw(ship, Left + _sixShips[i].X - ship.Width / 2, Top + _sixShips[i].Y - ship.Height / 2);
                }
            }

            return;
        }

        int[] offsets = [1101, 1301, 1401];
        int[] xs = [15, 95, 180];
        for (var i = 0; i < offsets.Length; i++)
        {
            if (combat.Get((Color * 1000) + offsets[i]) is { } ship)
            {
                target.Draw(ship, Left + xs[i], Top + (Height - ship.Height) / 2);
            }
        }
    }

    /// <summary>A layer repeated across the panel, moved left by an offset.</summary>
    private void Tile(Framebuffer target, Sprite? layer, int offset)
    {
        if (layer is null || layer.Width == 0)
        {
            return;
        }

        for (var x = Left - (offset % layer.Width); x < Left + Width; x += layer.Width)
        {
            for (var y = Top; y < Top + Height; y += layer.Height)
            {
                target.Draw(layer, x, y);
            }
        }
    }
}
