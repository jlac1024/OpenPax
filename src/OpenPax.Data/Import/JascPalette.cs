using System.Globalization;
using System.Text;
using OpenPax.Data.Art;

namespace OpenPax.Data.Import;

/// <summary>
/// Writes palettes as JASC-PAL text, a plain format that GIMP, Aseprite and
/// most palette tools open.
/// </summary>
public static class JascPalette
{
    public static string Format(Palette palette)
    {
        var text = new StringBuilder();
        text.Append("JASC-PAL\n0100\n256\n");
        foreach (var c in palette.Colors)
        {
            text.Append(c.R).Append(' ').Append(c.G).Append(' ').Append(c.B).Append('\n');
        }

        return text.ToString();
    }

    /// <summary>Reads JASC-PAL text as written by <see cref="Format"/>.</summary>
    public static Palette Parse(string text)
    {
        var lines = text.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (lines.Length < 3 || lines[0] != "JASC-PAL" || !int.TryParse(lines[2], CultureInfo.InvariantCulture, out var count) || lines.Length < 3 + count)
        {
            throw new FormatException(Strings.BadPalette);
        }

        var colors = new Rgb[count];
        for (var i = 0; i < count; i++)
        {
            var parts = lines[3 + i].Split(' ', StringSplitOptions.RemoveEmptyEntries);
            colors[i] = new Rgb(
                byte.Parse(parts[0], CultureInfo.InvariantCulture),
                byte.Parse(parts[1], CultureInfo.InvariantCulture),
                byte.Parse(parts[2], CultureInfo.InvariantCulture));
        }

        return new Palette(colors);
    }
}
