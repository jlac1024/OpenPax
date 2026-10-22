namespace OpenPax.Ui.Widgets;

/// <summary>A caption in a list, in its own colors (draw).</summary>
public sealed class CaptionRow(string text, byte color, byte selectedColor) : TextRow
{
    protected override int Indent => 3;

    protected override byte Color => color;

    protected override byte SelectedColor => selectedColor;

    public override string Text(UiAssets assets) => text;
}
