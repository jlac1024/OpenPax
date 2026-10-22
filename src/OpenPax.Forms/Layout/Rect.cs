namespace OpenPax.Forms.Layout;

/// <summary>A rectangle in layout units, from the form's top left.</summary>
public readonly record struct Rect(int X, int Y, int Width, int Height)
{
    public int Right => X + Width;

    public int Bottom => Y + Height;

    public bool Contains(int x, int y) => x >= X && y >= Y && x < Right && y < Bottom;

    public Rect Deflate(Thickness t) => new(X + t.Left, Y + t.Top, Math.Max(0, Width - t.Horizontal), Math.Max(0, Height - t.Vertical));
}
