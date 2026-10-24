using OpenPax.Render;

namespace OpenPax.Ui;

/// <summary>
/// The game's own animated pointer: a set of 20 Main.img
/// pictures from the set's number on, a picture every 50 ms, drawn over
/// everything with the set's hot spot under the mouse. Screens and widgets
/// pick the set; it stays until something picks another.
/// </summary>
public sealed class MouseCursor
{
    public const int Arrow = 50;
    public const int Slide = 90;
    public const int Target = 110;
    public const int Attack = 130;
    public const int ListDrag = 150;
    public const int GameStart = 170;
    public const int Bridge = 190;
    public const int SpeciesSelection = 210;
    public const int Colonize = 230;
    public const int GameStartSecond = 270;
    public const int CannotColonize = 290;
    public const int CannotAttack = 310;
    public const int PlayerDesign = 330;
    public const int Victory = 370;
    public const int Defeat = 390;

    private const int Frames = 20;
    private const int FramePeriod = 50;

    private int _frame;
    private long _lastFrame;

    /// <summary>The set shown, the number of its first picture.</summary>
    public int Set { get; private set; } = Arrow;

    /// <summary>
    /// The port's own: the set the screen up picked for itself (the arrow
    /// when it picks none). What a popup or the map's "no order" goes back
    /// to: the bridge's own pointer, not the arrow, which its palette draws
    /// in the wrong colors.
    /// </summary>
    public int Home { get; set; } = Arrow;

    /// <summary>
    /// Picks a set. Quirk kept: the picture count is not reset,
    /// so a new set starts at whatever picture the last one had reached.
    /// </summary>
    public void Show(int set)
    {
        Set = set;
    }

    /// <summary>The point of each set that sits on the mouse: arrows at their tip, cross hairs at their middle.</summary>
    public static (int X, int Y) HotSpot(int set) => set switch
    {
        70 or Slide or Target or ListDrag or 350 => (14, 14),
        Attack or Colonize or 250 or CannotColonize or CannotAttack => (15, 15),
        Arrow or GameStart or Bridge or SpeciesSelection or GameStartSecond or PlayerDesign or Victory or Defeat => (7, 2),
        _ => (0, 0),
    };

    /// <summary>The pointer on the true-color canvas at a canvas point, at the scale the original screens are shown at.</summary>
    public void Draw(Render.TrueColor.Canvas canvas, UiAssets assets, Data.Art.Palette palette, int x, int y, int scale, long now)
    {
        Advance(now);
        if (assets.Pictures(ImageSet.Main).Get(Set + _frame) is { } picture)
        {
            var (hx, hy) = HotSpot(Set);
            canvas.DrawImage(Render.TrueColor.RgbaImage.From(picture, palette), x - hx * scale, y - hy * scale, scale);
        }
    }

    private void Advance(long now)
    {
        if (now - _lastFrame >= FramePeriod)
        {
            _lastFrame = now;
            _frame = (_frame + 1) % Frames;
        }
    }

    /// <summary>Draws the pointer at the mouse, stepping the animation when its time has come.</summary>
    public void Draw(Framebuffer target, UiAssets assets, int x, int y, long now)
    {
        Advance(now);

        if (assets.Pictures(ImageSet.Main).Get(Set + _frame) is { } picture)
        {
            var (hx, hy) = HotSpot(Set);
            target.Draw(picture, x - hx, y - hy);
        }
    }
}
