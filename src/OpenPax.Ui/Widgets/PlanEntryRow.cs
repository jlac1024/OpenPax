using OpenPax.Data.Views;

namespace OpenPax.Ui.Widgets;

/// <summary>An improvement in a plan, in the profile editor: its name.</summary>
public sealed class PlanEntryRow(int techId) : TextRow
{
    public int TechId { get; } = techId;

    public override string Text(UiAssets assets) => assets.Strings.Get(new TextRef(100000 + TechId, 0));
}
