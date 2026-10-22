using OpenPax.Data.Art;

namespace OpenPax.Data.Png;

/// <summary>
/// An 8-bit indexed image as stored in our PNGs: pixels are palette
/// indices, the palette has up to 256 entries, and <see cref="Alpha"/>
/// gives per-index opacity (indices past its end are opaque).
/// </summary>
public sealed record IndexedImage(int Width, int Height, byte[] Pixels, IReadOnlyList<Rgb> Palette, IReadOnlyList<byte> Alpha);
