using OpenPax.Data.Views;
using OpenPax.Sim.Players;

namespace OpenPax.Ui.Widgets;

/// <summary>A trade package waiting for the player: a gift, a demand or a trade, and from whom.</summary>
public sealed class PackageRow(TradePackage package) : TextRow
{
    public TradePackage Package { get; } = package;

    public override string Text(UiAssets assets)
    {
        var entry = Package.IsGift ? 2 : Package.IsDemand ? 3 : 4;
        return assets.Strings.Get(new TextRef(85, entry)) + Package.Proposer.Name;
    }
}
