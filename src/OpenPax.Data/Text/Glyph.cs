namespace OpenPax.Data.Text;

/// <summary>
/// One character of a bitmap font: <see cref="Width"/> pixels wide, with
/// rows of 1-bit pixels padded to whole bytes, most significant bit first.
/// A glyph with width 0 has no bitmap.
/// </summary>
public sealed record Glyph(int Code, int Width, byte[] Bits)
{
    public int BytesPerRow => (Width + 7) >> 3;

    public int Rows => BytesPerRow == 0 ? 0 : Bits.Length / BytesPerRow;

    public bool IsSet(int x, int y)
    {
        return (Bits[y * BytesPerRow + (x >> 3)] & (0x80 >> (x & 7))) != 0;
    }
}
