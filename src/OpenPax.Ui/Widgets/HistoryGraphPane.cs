using OpenPax.Data.Views;
using OpenPax.Render;
using OpenPax.Sim;
using OpenPax.Sim.Players;

namespace OpenPax.Ui.Widgets;

/// <summary>
/// Class 8040, the empire history graph: one statistic's 50 samples for
/// each player shown, a line each in the player's color, all scaled to
/// the highest sample among the players shown.
/// </summary>
public sealed class HistoryGraphPane(ViewNode node, int left, int top) : Pane(node, left, top)
{
    public const int Players = 16;

    private readonly bool[] _shown = new bool[Players];

    public Game? Game { get; set; }

    /// <summary>The statistic, 1 to 10 (+0x5C); the history's index plus one.</summary>
    public int Statistic { get; private set; } = 1;

    /// <summary>Whether a player's line is drawn, by player index (+0x4C).</summary>
    public bool LineShown(int index) => _shown[index];

    public void Show(int index, bool shown)
    {
        _shown[index] = shown;
    }

    /// <summary>One statistic on or back, wrapping between 1 and 10.</summary>
    public void Step(int by)
    {
        Statistic += by;
        if (Statistic > 10)
        {
            Statistic = 1;
        }

        if (Statistic < 1)
        {
            Statistic = 10;
        }
    }

    private IReadOnlyList<int>? Series(int index)
    {
        return Game is { } game && index < game.Players.Count ? game.Players[index].History.Series(Statistic - 1) : null;
    }

    /// <summary>Each shown player's samples scaled to the pane's height, -1 kept for no sample.</summary>
    public int[]?[] Scaled()
    {
        var top = 0.0;
        for (var i = 0; i < Players; i++)
        {
            if (_shown[i] && Series(i) is { } s)
            {
                top = Math.Max(top, s.Max(v => (double)v));
            }
        }

        if (top == 0.0)
        {
            top = 1.0;
        }

        var scaled = new int[]?[Players];
        for (var i = 0; i < Players; i++)
        {
            if (_shown[i] && Series(i) is { } s)
            {
                scaled[i] = [.. s.Select(v => v == EmpireHistory.Empty ? -1 : Sim.Numerics.X87.ToInt(v * (Height / top)))];
            }
        }

        return scaled;
    }

    /// <summary>
    /// From the left edge, a sample every width / 50 px, measured up from the
    /// bottom; a line stops before the first missing sample.
    /// </summary>
    protected override void Draw(Framebuffer target)
    {
        base.Draw(target);
        var scaled = Scaled();
        var step = Width / EmpireHistory.Samples;
        for (var i = 0; i < Players; i++)
        {
            if (scaled[i] is not { } line)
            {
                continue;
            }

            var color = PlayerColors.For(i);
            var (x, y) = (Left, Top + Height - line[0]);
            for (var k = 1; k < line.Length; k++)
            {
                var (nx, ny) = (x + step, Top + Height - line[k]);
                target.DrawLine(x, y, nx, ny, color);
                (x, y) = (nx, ny);
                if (k + 1 < line.Length && line[k + 1] < 0)
                {
                    break;
                }
            }
        }
    }
}
