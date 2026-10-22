namespace OpenPax.Ui.Widgets;

/// <summary>The color each player's line and legend box take in the history graph, by player index.</summary>
internal static class PlayerColors
{
    private static readonly byte[] _colors = [0xFE, 0x91, 0xC9, 0x8C, 0xD1, 0x0F, 0xBD, 0x99, 0x62, 0x26, 0x30, 0x85, 0xA3, 0x45, 0x08, 0x72];

    public static byte For(int index) => _colors[index % _colors.Length];
}
