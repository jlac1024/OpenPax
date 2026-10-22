namespace OpenPax.Forms.Drawing;

/// <summary>Breaks text into lines that fit a width: at spaces, at line feeds, and inside a word too long for a line.</summary>
public static class TextWrap
{
    /// <param name="measure">A line's width as drawn.</param>
    public static IReadOnlyList<string> Lines(Func<string, int> measure, string text, int width)
    {
        var lines = new List<string>();
        foreach (var paragraph in text.Replace("\r", "", StringComparison.Ordinal).Split('\n'))
        {
            var line = "";
            foreach (var word in paragraph.Split(' '))
            {
                var next = line.Length == 0 ? word : line + " " + word;
                if (measure(next) <= width || line.Length == 0 && measure(word) <= width)
                {
                    line = next;
                    continue;
                }

                if (line.Length > 0)
                {
                    lines.Add(line);
                }

                line = word;
                while (line.Length > 1 && measure(line) > width)
                {
                    var cut = line.Length - 1;
                    while (cut > 1 && measure(line[..cut]) > width)
                    {
                        cut--;
                    }

                    lines.Add(line[..cut]);
                    line = line[cut..];
                }
            }

            lines.Add(line);
        }

        return lines;
    }
}
