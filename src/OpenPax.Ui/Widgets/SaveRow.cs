namespace OpenPax.Ui.Widgets;

/// <summary>A saved game in the load and save lists: its name, 14, or 22 when picked.</summary>
public sealed class SaveRow(string name) : TextRow
{
    public string Name { get; } = name;

    protected override int Indent => 3;

    protected override byte Color => 14;

    protected override byte SelectedColor => 22;

    public override string Text(UiAssets assets) => Name;
}
