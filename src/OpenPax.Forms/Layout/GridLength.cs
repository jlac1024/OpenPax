using System.Globalization;

namespace OpenPax.Forms.Layout;

/// <summary>A grid row's or column's size: fixed units, Auto (its content), or a share of what is left ("*", "2*").</summary>
public readonly record struct GridLength(int Value, GridUnit Unit)
{
    public static GridLength Auto => new(0, GridUnit.Auto);

    public static GridLength Star => new(1, GridUnit.Star);

    public static bool TryParse(string text, out GridLength length)
    {
        text = text.Trim();
        length = Star;
        if (text.Equals("Auto", StringComparison.OrdinalIgnoreCase))
        {
            length = Auto;
            return true;
        }

        if (text.EndsWith('*'))
        {
            var share = text[..^1];
            length = new GridLength(share.Length == 0 ? 1 : int.TryParse(share, NumberStyles.None, CultureInfo.InvariantCulture, out var s) ? s : -1, GridUnit.Star);
            return length.Value > 0;
        }

        length = new GridLength(int.TryParse(text, NumberStyles.None, CultureInfo.InvariantCulture, out var v) ? v : -1, GridUnit.Pixel);
        return length.Value >= 0;
    }

    /// <summary>A list such as "Auto,*,2*,40".</summary>
    public static IReadOnlyList<GridLength> ParseList(string text) =>
        [.. text.Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries).Select(t => TryParse(t, out var l) ? l : Star)];
}
