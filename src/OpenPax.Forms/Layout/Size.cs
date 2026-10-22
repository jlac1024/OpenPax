namespace OpenPax.Forms.Layout;

/// <summary>A width and height in layout units; <see cref="Infinite"/> where there is no limit.</summary>
public readonly record struct Size(int Width, int Height)
{
    public const int Infinite = int.MaxValue / 4;

    public Size Deflate(Thickness t) => new(Math.Max(0, Width - t.Horizontal), Math.Max(0, Height - t.Vertical));

    public Size Inflate(Thickness t) => new(Width + t.Horizontal, Height + t.Vertical);
}
