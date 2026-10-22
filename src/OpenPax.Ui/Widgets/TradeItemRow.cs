using System.Globalization;
using OpenPax.Data.Views;
using OpenPax.Sim;

namespace OpenPax.Ui.Widgets;

/// <summary>
/// One thing in a trade package: a planet, a ship, credits,
/// the star map or a tech, by kind 0 to 4.
/// </summary>
public sealed class TradeItemRow(Game game, int kind, int value) : TextRow
{
    public const int PlanetItem = 0;
    public const int ShipItem = 1;
    public const int CreditsItem = 2;
    public const int MapItem = 3;
    public const int TechItem = 4;

    public int Kind { get; } = kind;

    public int Value { get; } = value;

    protected override int Indent => 10;

    public override string Text(UiAssets assets)
    {
        return Kind switch
        {
            PlanetItem => game.Universe.Stars.SelectMany(s => s.Planets).FirstOrDefault(p => p.ObjectId == Value)?.Name ?? "",
            ShipItem => game.FindShip(Value) is { } ship ? ship.ClassName + ": " + ship.Name : "",
            CreditsItem => assets.Strings.Get(new TextRef(104, 0)) + Value.ToString(CultureInfo.InvariantCulture),
            MapItem => assets.Strings.Get(new TextRef(85, 1)),
            _ => assets.Strings.Get(new TextRef(100000 + Value, 0)),
        };
    }
}
