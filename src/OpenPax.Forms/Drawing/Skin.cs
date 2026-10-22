using OpenPax.Render.TrueColor;

namespace OpenPax.Forms.Drawing;

/// <summary>A piece of art drawn as a nine-slice: corners kept, edges and middle stretched. Insets are in the art's own pixels.</summary>
public sealed record Skin(RgbaImage Image, int Left, int Top, int Right, int Bottom)
{
    /// <summary>The art drawn whole, scaled, without slicing (arrows, check marks).</summary>
    public static Skin Whole(RgbaImage image) => new(image, 0, 0, 0, 0);
}
