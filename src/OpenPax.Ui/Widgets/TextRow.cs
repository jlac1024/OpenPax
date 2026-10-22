using OpenPax.Render;

namespace OpenPax.Ui.Widgets;

/// <summary>
/// A plain line of text in a list (the item base with no color
/// of its own): color 9, or 255 when picked, at the row's left. Rows with
/// their own colors, such as the captions of the offer popup, give them.
/// </summary>
public abstract class TextRow : ListRow
{
    protected virtual int Indent => 0;

    protected virtual byte Color => 9;

    protected virtual byte SelectedColor => 255;

    /// <summary>The row's words, read when drawn so they follow the game.</summary>
    public abstract string Text(UiAssets assets);

    public override void Draw(Framebuffer target, UiAssets assets, int x, int y, int width, bool selected)
    {
        var font = assets.Fonts.ForIndex(1);
        var baseline = y + Height - (font.LineHeight - font.AscentFor(0));
        font.Draw(target, Text(assets), x + Indent, baseline, 0, 0, selected ? SelectedColor : Color, 0);
    }

    /// <summary>These lists have no sort key.</summary>
    public override int Compare(ListRow other, int sortCode)
    {
        return 0;
    }
}
