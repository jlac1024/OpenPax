using OpenPax.Data.Views;
using OpenPax.Render;

namespace OpenPax.Ui.Widgets;

/// <summary>
/// Class 10003 in Game Setup: the chosen flag large between its three
/// neighbors on each side, small.
/// </summary>
public sealed class FlagPane(ViewNode node, int left, int top, ByteOptionDetails details) : Pane(node, left, top)
{
    public const int Flags = 30;

    public int Flag { get; private set; }

    public void Set(int flag)
    {
        Flag = ((flag % Flags) + Flags) % Flags;
    }

    public void Step(int by)
    {
        Set(Flag + by);
    }

    protected override void Draw(Framebuffer target)
    {
        var empire = Assets.Pictures(ImageSet.Empire);
        if (details.Value == 0)
        {
            if (empire.Get(3000 + Flag) is { } only)
            {
                target.Draw(only, Left + (Width - only.Width) / 2, Top + (Height - only.Height) / 2);
            }

            return;
        }

        var x = Left + 6;
        for (var k = Flag - 3; k <= Flag + 3; k++)
        {
            var flag = ((k % Flags) + Flags) % Flags;
            if (k == Flag)
            {
                if (empire.Get(3000 + flag) is { } large)
                {
                    target.Draw(large, x + 6, Top);
                }

                x += 18 + 40;
            }
            else
            {
                if (empire.Get(1000 + flag) is { } small)
                {
                    target.Draw(small, x, Top + 8);
                }

                x += 26;
            }
        }
    }
}
