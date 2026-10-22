using OpenPax.Data.Views;
using OpenPax.Render;

namespace OpenPax.Ui.Widgets;

/// <summary>Class 8009, one picture at the widget's top left; code may change the picture and its file.</summary>
public sealed class PicturePane(ViewNode node, int left, int top, PictureDetails details) : Pane(node, left, top)
{
    public int Picture { get; set; } = details.Picture;

    public ImageSet Set { get; set; } = ImageSet.Main;

    /// <summary>
    /// The port's own: a picture that is a sign, not panel art (Foreign
    /// Affairs' cross over an eliminated empire), keeps its shape in a pane a
    /// wide window has grown, centered where it was centered.
    /// </summary>
    public bool KeepShape { get; set; }

    protected override void Draw(Framebuffer target)
    {
        if (Picture == 0 || Assets.Pictures(Set).Get(Picture) is not { } sprite)
        {
            return;
        }

        if (KeepShape && Original is { } o)
        {
            target.Draw(sprite, Left + ((Width - o.Width) / 2), Top + ((Height - o.Height) / 2));
        }
        else
        {
            DrawFitted(target, sprite);
        }
    }
}
