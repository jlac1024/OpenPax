namespace OpenPax.Data.Text;

/// <summary>
/// One of a font's two glyph sets. <see cref="Metrics"/> are the 32-bit
/// values stored before the glyphs (two for the first set, one for the
/// second); what each means is settled when the text drawing code is ported.
/// </summary>
public sealed record GlyphSet(int First, int Last, IReadOnlyList<int> Metrics, IReadOnlyList<Glyph> Glyphs);
