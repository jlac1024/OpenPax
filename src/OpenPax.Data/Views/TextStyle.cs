namespace OpenPax.Data.Views;

/// <summary>
/// How text is drawn. Font is an index (bit 0x80 is ignored; 1, 2, 3, 4 and
/// 6 are distinct fonts, anything else the default font).
/// Style bit 4 draws a shadow at (+1, +1) in <see cref="ShadowColor"/>; bit 1
/// picks the font's other weight. Align: 0 left, 1 center, 2 right. Colors
/// are palette indices.
/// </summary>
public sealed record TextStyle(int Style, int Align, int Font, int Color, int ShadowColor);
