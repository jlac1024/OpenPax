using OpenPax.Sim.Combat;
using OpenPax.Sim.Numerics;

namespace OpenPax.Sim;

/// <summary>
/// Group moves and formations in a battle. Each
/// only sends every ship to a slot point; nothing keeps the formation after.
/// Slots and ships are sorted the same way so the nearest ship takes each
/// slot; the sort is the C library's, unstable as in the original.
/// </summary>
public sealed partial class Game
{
    private const int FormationSpacing = 100;
    private const double DiagonalStep = 5.0 / 6.0;

    private static int ByX((int X, int Y) a, (int X, int Y) b) => a.X != b.X ? (a.X < b.X ? -1 : 1) : a.Y != b.Y ? (a.Y < b.Y ? -1 : 1) : 0;

    private static int ByY((int X, int Y) a, (int X, int Y) b) => a.Y != b.Y ? (a.Y < b.Y ? -1 : 1) : a.X != b.X ? (a.X < b.X ? -1 : 1) : 0;

    private static int ShipsByX(BattleShip a, BattleShip b) => ByX((a.X, a.Y), (b.X, b.Y));

    private static int ShipsByY(BattleShip a, BattleShip b) => ByY((a.X, a.Y), (b.X, b.Y));

    /// <summary>The middle of the box round the ships; (0, 0) for none.</summary>
    private static (int X, int Y) SelectionCenter(List<BattleShip> ships)
    {
        if (ships.Count == 0)
        {
            return (0, 0);
        }

        var l = ships.Min(s => s.X - (s.Radius >> 1));
        var t = ships.Min(s => s.Y - (s.Radius >> 1));
        var r = ships.Max(s => s.X + (s.Radius >> 1));
        var b = ships.Max(s => s.Y + (s.Radius >> 1));
        return ((r - l) / 2 + l, (b - t) / 2 + t);
    }

    /// <summary>Whether the ships go mostly sideways from where they are to the point.</summary>
    private static bool Lateral(List<BattleShip> ships, (int X, int Y) point)
    {
        var c = SelectionCenter(ships);
        var h = X87.ToInt(Direction(c.X, c.Y, point.X, point.Y));
        return (h > 45 && h <= 135) || (h > 225 && h <= 315);
    }

    /// <summary>Sorts slots and ships alike and sends each ship to its slot.</summary>
    private static void Assign(BattleShip[] ships, (int X, int Y)[] slots, bool lateral)
    {
        MsvcSort.Sort<(int X, int Y)>(slots, lateral ? new Comparison<(int X, int Y)>(ByX) : ByY);
        MsvcSort.Sort<BattleShip>(ships, lateral ? new Comparison<BattleShip>(ShipsByX) : ShipsByY);
        for (var k = 0; k < ships.Length; k++)
        {
            SendTo(ships[k], slots[k]);
        }
    }

    /// <summary>
    /// A group move (kind 1): rows of five across the way of travel, rows
    /// behind; the ships nearest the point fill the front. Quirk kept: a
    /// slot's sideways offset is turned with the already rounded forward
    /// offset, so rows come out skewed.
    /// </summary>
    private void GroupMove(List<BattleShip> selection, (int X, int Y) point)
    {
        var n = selection.Count;
        var ships = selection.ToArray();
        MsvcSort.Sort<BattleShip>(ships, (a, b) =>
        {
            var da = Distance(point.X, point.Y, a.X, a.Y);
            var db = Distance(point.X, point.Y, b.X, b.Y);
            return da < db ? -1 : da > db ? 1 : 0;
        });

        var c = SelectionCenter(selection);
        var h = X87.ToInt(Direction(c.X, c.Y, point.X, point.Y));
        var lateral = (h > 45 && h <= 135) || (h > 225 && h <= 315);
        var slots = new (int X, int Y)[n];
        var s = 1;
        for (var i = 0; i < n; i++)
        {
            var side = (double)((i % 5 + 1) / 2 * s * FormationSpacing);
            var back = (double)(i / 5 * -FormationSpacing);
            var dx = X87.ToInt(back * Tables.Cos[h] - Tables.Sin[h] * side - -0.5);
            var dy = X87.ToInt(dx * Tables.Sin[h] + Tables.Cos[h] * side - -0.5);
            slots[i] = (Math.Min(Math.Max(point.X + dx, 100), 3500), Math.Min(Math.Max(point.Y + dy, 100), 3500));
            s = -s;
        }

        for (var k = 0; k < n; k += 5)
        {
            var m = n - k < 6 ? n - k : 5;
            var rowShips = ships.AsSpan(k, m).ToArray();
            var rowSlots = slots.AsSpan(k, m).ToArray();
            Assign(rowShips, rowSlots, lateral);
        }

    }

    /// <summary>Half a line's length, in the original's byte arithmetic.</summary>
    private static int HalfLine(int n) => (byte)(n + 1) * FormationSpacing / 2;

    /// <summary>A line across (kind 9): centered on the point, kept off the edges. Quirk kept: the right limit is 3650.</summary>
    private static void LineAcross(List<BattleShip> selection, (int X, int Y) point)
    {
        var half = HalfLine(selection.Count);
        var px = Math.Min(Math.Max(point.X, half + 50), 3650 - half);
        var py = Math.Min(Math.Max(point.Y, 50), 3550);
        var slots = new (int X, int Y)[selection.Count];
        var s = 1;
        for (var i = 0; i < slots.Length; i++)
        {
            slots[i] = (px + (i + 1) / 2 * s * FormationSpacing, py);
            s = -s;
        }

        Assign([.. selection], slots, lateral: true);
    }

    /// <summary>A line down (kind 10).</summary>
    private static void LineDown(List<BattleShip> selection, (int X, int Y) point)
    {
        var half = HalfLine(selection.Count);
        var px = Math.Min(Math.Max(point.X, 50), 3550);
        var py = Math.Min(Math.Max(point.Y, half + 50), 3600 - half);
        var slots = new (int X, int Y)[selection.Count];
        var s = 1;
        for (var i = 0; i < slots.Length; i++)
        {
            slots[i] = (px, py + (i + 1) / 2 * s * FormationSpacing);
            s = -s;
        }

        Assign([.. selection], slots, lateral: false);
    }

    /// <summary>
    /// A diagonal (kind 11 rising to the right, 12 falling): slots 5/6 of the
    /// spacing apart each way. Ships and slots are sorted by how the ships
    /// travel to the point as given, before it is kept off the edges.
    /// </summary>
    private static void Diagonal(List<BattleShip> selection, (int X, int Y) point, bool left)
    {
        var n = selection.Count;
        var half = HalfLine(n);
        var px = Math.Min(Math.Max(point.X, 50), 3550);
        var py = Math.Min(Math.Max(point.Y, half + 50), 3600 - half);
        var slots = new (int X, int Y)[n];
        var s = 1;
        for (var j = 1; j <= n; j++)
        {
            var k = j / 2;
            if (left)
            {
                var v = X87.ToInt(k * s * FormationSpacing * DiagonalStep - -0.5);
                slots[j - 1] = (px + v, py + v);
            }
            else
            {
                var dy = X87.ToInt(-k * s * FormationSpacing * DiagonalStep - -0.5);
                var dx = X87.ToInt(k * s * FormationSpacing * DiagonalStep - -0.5);
                slots[j - 1] = (px + dx, py + dy);
            }

            s = -s;
        }

        Assign([.. selection], slots, Lateral(selection, point));
    }
}
