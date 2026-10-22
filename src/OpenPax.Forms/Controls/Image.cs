using OpenPax.Forms.Drawing;
using OpenPax.Forms.Layout;

namespace OpenPax.Forms.Controls;

/// <summary>A picture by name from the theme's art ("Main:44300", or a mod's), at its own size times the scale, or stretched when Stretch is set.</summary>
public sealed class Image : Element
{
    public string Source { get; set; } = "";

    public bool Stretch { get; set; }

    protected override Size MeasureOverride(Size available) =>
        Theme.Art.Get(Source) is { } image ? new Size(image.Width, image.Height) : default;

    protected override void OnRender(DrawContext dc)
    {
        if (Theme.Art.Get(Source) is not { } image)
        {
            return;
        }

        if (Stretch)
        {
            dc.Skin(Skin.Whole(image), Bounds);
        }
        else
        {
            dc.ImageCentered(image, Bounds);
        }
    }
}
