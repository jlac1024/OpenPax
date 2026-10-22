namespace OpenPax.Data.Text;

/// <summary>
/// Windows-1252, the character set the game's fonts are laid out in (codes
/// 0x80 to 0x9F hold the curly quotes, dashes, the euro-less Windows extras).
/// Written out here because .NET only ships it through an extra provider.
/// </summary>
public static class Windows1252
{
    // 0x80..0x9F. Codes undefined in Windows-1252 map to the C1 control of the same value.
    private static readonly char[] _high =
    [
        '€', '\u0081', '‚', 'ƒ', '„', '…', '†', '‡',
        'ˆ', '‰', 'Š', '‹', 'Œ', '\u008D', 'Ž', '\u008F',
        '\u0090', '‘', '’', '“', '”', '•', '–', '—',
        '˜', '™', 'š', '›', 'œ', '\u009D', 'ž', 'Ÿ',
    ];

    public static string GetString(ReadOnlySpan<byte> bytes)
    {
        return string.Create(bytes.Length, bytes.ToArray(), static (chars, src) =>
        {
            for (var i = 0; i < src.Length; i++)
            {
                var b = src[i];
                chars[i] = b is >= 0x80 and <= 0x9F ? _high[b - 0x80] : (char)b;
            }
        });
    }

    /// <summary>The Windows-1252 code for a character, or null when it has none.</summary>
    public static byte? GetByte(char c)
    {
        if (c < 0x80 || c is >= (char)0xA0 and <= (char)0xFF)
        {
            return (byte)c;
        }

        var i = Array.IndexOf(_high, c);
        return i < 0 ? null : (byte)(0x80 + i);
    }
}
