using OpenPax.Data.Views;
using OpenPax.Render;
using OpenPax.Sim.Players;

namespace OpenPax.Ui.Widgets;

/// <summary>
/// Class 9002: a picture turning at 66 ms, centered. Mode 1 a tech (36
/// pictures of Tech.img); mode 2 a hull turning in the local player's
/// color (32 pictures of Ships.img); mode 3 an armor class (36 pictures
/// of the armor tech's animation). The frame runs on across changes.
/// </summary>
public sealed class TechPicturePane(ViewNode node, int left, int top) : Pane(node, left, top)
{
    private const int FrameTime = 66;
    private const int Frames = 36;
    private const int ShipFrames = 32;

    private long _now;

    /// <summary>1 tech, 2 hull, 3 armor class.</summary>
    public int Mode { get; set; } = 1;

    /// <summary>The tech shown in mode 1.</summary>
    public TechInfo? Tech { get; set; }

    /// <summary>The hull type (mode 2, 0 to 5) or armor class (mode 3, 0 to 4); anything else shows nothing.</summary>
    public int Value { get; set; } = -1;

    /// <summary>The local player's fleet color, for the hull (mode 2).</summary>
    public int FleetColor { get; set; }

    /// <summary>The armor tech's first picture, for mode 3 (+0x5C).</summary>
    public int ArmorPicture { get; set; }

    protected internal override void Tick(long now)
    {
        _now = now;
    }

    protected override void Draw(Framebuffer target)
    {
        var frame = (int)(_now / FrameTime);
        var (x, y) = (Left + (Width / 2), Top + (Height / 2));
        switch (Mode)
        {
            case 1 when Tech is { } tech:
                MapPane.DrawCentered(target, Assets.Pictures(ImageSet.Tech).Get(tech.Picture + (frame % Frames)), x, y);
                break;
            case 2 when Value is >= 0 and < 6:
                // Quirk kept: color * 10 here, unlike the other ship pictures.
                MapPane.DrawCentered(target, Assets.Pictures(ImageSet.Ships).Get(((Value + (FleetColor * 10)) * 100) + 1001 + (frame % ShipFrames)), x, y);
                break;
            case 3 when Value is >= 0 and < 5:
                MapPane.DrawCentered(target, Assets.Pictures(ImageSet.Tech).Get(ArmorPicture + (Value * 50) + (frame % Frames)), x, y);
                break;
            default:
                break;
        }
    }
}
