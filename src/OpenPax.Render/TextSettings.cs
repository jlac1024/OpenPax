using OpenPax.Render.Fonts;

namespace OpenPax.Render;

/// <summary>The text look every game font of a library shares; changed while running for comparing.</summary>
public sealed class TextSettings
{
    public TextMode Mode { get; set; } = TextMode.Original;

    public TextOutline Outline { get; set; } = TextOutline.None;

    /// <summary>The modern font, when its files are there.</summary>
    public ModernFont? Modern { get; init; }
}
