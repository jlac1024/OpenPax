namespace OpenPax.Ui.Widgets;

/// <summary>A saved game in the load and save lists: its name, 14, or 22 when picked.</summary>
public sealed class SaveRow(string name) : TextRow
{
    public string Name { get; } = name;

    /// <summary>
    /// The port's own: what is wrong with the save's mods (some not turned
    /// on, or at another version), shown as an orange warning at the row's
    /// end with this as its tooltip; null when nothing is.
    /// </summary>
    public string? Warning { get; init; }

    /// <summary>The port's own: the save needs mods that are not turned on, so LOAD is off for it.</summary>
    public bool Blocked { get; init; }

    protected override int Indent => 3;

    protected override byte Color => 14;

    protected override byte SelectedColor => 22;

    public override string Text(UiAssets assets) => Name;
}
