using OpenPax.Data.Views;
using OpenPax.Sim.Players;

namespace OpenPax.Ui.Widgets;

/// <summary>A treaty offered to the player: the offerer's name and the treaty.</summary>
public sealed class TreatyOfferRow(Player from, int state) : TextRow
{
    // The treaty states' words in STR# 15000, War to Share Information.
    private static readonly int[] _words = [1, 2, 12, 11, 3, 13, 14];

    public Player From { get; } = from;

    public int State { get; } = state;

    public static string Word(UiAssets assets, int state) => assets.Strings.Get(new TextRef(15000, _words[state]));

    public override string Text(UiAssets assets) => From.Name + " " + Word(assets, State);
}
