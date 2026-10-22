using System.Globalization;

namespace OpenPax.Forms.Layout;

/// <summary>Space round an element (margin) or inside it (padding), in layout units.</summary>
public readonly record struct Thickness(int Left, int Top, int Right, int Bottom)
{
    public Thickness(int all)
        : this(all, all, all, all)
    {
    }

    public int Horizontal => Left + Right;

    public int Vertical => Top + Bottom;

    /// <summary>"4", "4,2" (sides, then top and bottom) or "1,2,3,4" (left, top, right, bottom), as XAML writes them.</summary>
    public static bool TryParse(string text, out Thickness thickness)
    {
        thickness = default;
        var parts = text.Split(',', StringSplitOptions.TrimEntries);
        var values = new int[parts.Length];
        for (var i = 0; i < parts.Length; i++)
        {
            if (!int.TryParse(parts[i], NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out values[i]))
            {
                return false;
            }
        }

        thickness = values.Length switch
        {
            1 => new Thickness(values[0]),
            2 => new Thickness(values[0], values[1], values[0], values[1]),
            4 => new Thickness(values[0], values[1], values[2], values[3]),
            _ => default,
        };
        return values.Length is 1 or 2 or 4;
    }
}
