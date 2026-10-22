using OpenPax.Data.Views;
using OpenPax.Render;

namespace OpenPax.Ui.Widgets;

/// <summary>
/// Class 22002, the bridge's line showing the log's current notice. A double
/// click sends the line's id, and the bridge runs the notice's action.
/// </summary>
public sealed class MessageLinePane(ViewNode node, int left, int top) : Pane(node, left, top)
{
    private bool _double;

    public string Text { get; set; } = "";

    /// <summary>0 normal, 1 and 2 more urgent: colors 0x7E, 0xB2, 0xE0.</summary>
    public int Priority { get; set; }

    protected internal override void MouseDown(int x, int y, int clicks)
    {
        _double = clicks >= 2;
    }

    protected internal override void MouseUp(int x, int y)
    {
        if (_double && Text.Length > 0)
        {
            Post(Id, 0);
        }

        _double = false;
    }

    protected override void Draw(Framebuffer target)
    {
        var color = Priority switch
        {
            1 => (byte)0xB2,
            2 => (byte)0xE0,
            _ => (byte)0x7E,
        };

        // The text font is size 10, placed by the line height of size 11.
        Assets.Fonts.ForIndex(1).Draw(target, Text, Left + 5, Top + Assets.Fonts.ForIndex(2).LineHeight, 0, 0, color, 0);
    }
}
