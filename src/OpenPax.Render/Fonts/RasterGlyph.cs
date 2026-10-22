namespace OpenPax.Render.Fonts;

/// <summary>A glyph filled at a size: coverage 0 to 255, where its box sits from the pen (Top is rows above the baseline), and how far the pen moves.</summary>
public sealed record RasterGlyph(byte[] Coverage, int Width, int Height, int Left, int Top, float Advance);
