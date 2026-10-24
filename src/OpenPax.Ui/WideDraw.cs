using OpenPax.Render;

namespace OpenPax.Ui;

/// <summary>
/// Drawing an original picture larger than it is (docs/ui/widescreen.md):
/// cut across by the layout's column spans and down by its row spans, each
/// piece between spans drawn as it is, moved by the shares before it, and
/// each piece of a span stretched across (and down) its share more; the
/// tiled rows repeat at their period instead. Stretching leaves no joints:
/// repeating a strip showed seams, and mirroring it showed patterns.
/// </summary>
public static class WideDraw
{
    public static void Draw(Framebuffer target, Sprite picture, int x, int y, WideLayout layout, int extra, int extraHeight = 0)
    {
        if (extra <= 0 && extraHeight <= 0)
        {
            target.Draw(picture, x, y);
            return;
        }

        var across = WideLayout.Pieces(layout.Spans, layout.Shares(Math.Max(0, extra)), picture.Width);
        var down = WideLayout.Pieces(layout.Rows, layout.RowShares(Math.Max(0, extraHeight)), picture.Height);
        foreach (var (sy, sh, dy, dh) in down)
        {
            foreach (var (sx, sw, dx, dw) in across)
            {
                if (sw <= 0 || sh <= 0)
                {
                    continue;
                }

                if (layout.RepeatRows && dh > sh)
                {
                    // The span's rows again and again, the last copy cut short.
                    for (var k = 0; k < dh; k += sh)
                    {
                        var rows = Math.Min(sh, dh - k);
                        target.DrawRegion(picture, sx, sy, sw, rows, x + dx, y + dy + k, dw, rows, layout.Tiled);
                    }
                }
                else
                {
                    target.DrawRegion(picture, sx, sy, sw, sh, x + dx, y + dy, dw, dh, layout.Tiled);
                }
            }

            if (layout.RepeatRows && dh > sh && layout.SmoothColumns is { } smooth)
            {
                Smooth(target, picture, x, y, across, smooth, sy, sh, dy, dh);
            }
        }
    }

    // Each smooth column of a repeated span: its most common color in the
    // span, filled straight down the span's whole grown height.
    private static void Smooth(Framebuffer target, Sprite picture, int x, int y, List<(int From, int Length, int To, int ToLength)> across, IReadOnlyList<(int Left, int Right)> smooth, int sy, int sh, int dy, int dh)
    {
        var counts = new int[256];
        foreach (var (left, right) in smooth)
        {
            for (var column = Math.Max(0, left); column <= Math.Min(right, picture.Width - 1); column++)
            {
                // Only where the column is drawn as it is across (smooth columns lie outside the stretched spans).
                var piece = across.FindIndex(p => column >= p.From && column < p.From + p.Length && p.Length == p.ToLength);
                if (piece < 0)
                {
                    continue;
                }

                Array.Clear(counts);
                for (var row = sy; row < Math.Min(sy + sh, picture.Height); row++)
                {
                    counts[picture.Pixels[(row * picture.Width) + column]]++;
                }

                var color = (byte)Array.IndexOf(counts, counts.Max());
                target.FillRect(x + across[piece].To + (column - across[piece].From), y + dy, 1, dh, color);
            }
        }
    }
}
