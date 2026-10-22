using OpenPax.Render;

namespace OpenPax.Ui.Widgets;

/// <summary>The original's word wrapping into a rectangle.</summary>
internal static class WrappedText
{
    /// <summary>
    /// Splits on spaces and line feeds (runs collapse; a line feed is not a
    /// forced break), fills each line while it stays narrower than the
    /// rect, breaks a word that alone is too wide by characters, and stops
    /// when the next line's top reaches the bottom. Lines are placed by their
    /// cell top. Quirk kept: right alignment adds the absolute right edge.
    /// </summary>
    public static void Draw(Framebuffer target, BitmapText font, string text, int left, int top, int width, int height,
        int style, int align, byte color, byte shadow)
    {
        var words = new Queue<string>(text.Split([' ', '\n'], StringSplitOptions.RemoveEmptyEntries));
        var lineTop = top;
        var ascent = font.AscentFor(style);
        while (words.Count > 0 && lineTop < top + height)
        {
            var line = words.Dequeue();
            if (font.Measure(line, style) >= width)
            {
                var cut = 1;
                while (cut < line.Length && font.Measure(line[..(cut + 1)], style) < width)
                {
                    cut++;
                }

                if (cut < line.Length)
                {
                    words = new Queue<string>(words.Prepend(line[cut..]));
                    line = line[..cut];
                }
            }
            else
            {
                while (words.Count > 0 && font.Measure(line + " " + words.Peek(), style) < width)
                {
                    line += " " + words.Dequeue();
                }
            }

            var w = font.Measure(line, style);
            var x = align switch
            {
                1 => left + (width - w) / 2,
                2 => left + (left + width) - w,
                _ => left,
            };

            // BitmapText places by baseline; the original's line drawer takes the cell top.
            font.Draw(target, line, x, lineTop + ascent, style, 0, color, shadow);
            lineTop += font.LineHeight;
        }
    }
}
