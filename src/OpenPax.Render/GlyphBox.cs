namespace OpenPax.Render;

/// <summary>A glyph's place in its sheet.</summary>
public readonly record struct GlyphBox(int X, int Y, int Width, int Rows);
