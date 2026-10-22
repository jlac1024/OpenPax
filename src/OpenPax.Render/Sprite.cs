namespace OpenPax.Render;

/// <summary>An 8-bit picture of palette indices. Index 1 is never drawn, as in the original.</summary>
public sealed record Sprite(int Width, int Height, byte[] Pixels)
{
    public const byte TransparentIndex = 1;

    /// <summary>The picture remade at twice the size (art2x), for a framebuffer drawn at 2x; null where there is none.</summary>
    public Sprite? Hd { get; init; }
}
