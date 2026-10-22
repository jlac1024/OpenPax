namespace OpenPax.Render;

/// <summary>How text is drawn (decisions.md, "Upscaled art and a modern font"). Layout always uses the game font's own measures.</summary>
public enum TextMode
{
    /// <summary>The game's bitmap fonts, each pixel a square.</summary>
    Original,

    /// <summary>The game's bitmap fonts run through the 2x pixel-art upscaler.</summary>
    Upscaled,

    /// <summary>A modern outline font, anti-aliased, sized to the game font's capitals.</summary>
    Modern,
}
