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
        }
    }
}
