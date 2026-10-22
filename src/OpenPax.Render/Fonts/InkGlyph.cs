namespace OpenPax.Render.Fonts;

/// <summary>One glyph of a line ready to draw: coverage 0 to 255 and its top left in pixels.</summary>
public sealed record InkGlyph(byte[] Coverage, int Width, int Height, int X, int Y);
