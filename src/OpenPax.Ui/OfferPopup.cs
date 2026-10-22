using OpenPax.Data.Views;
using OpenPax.Sim.Commands;
using OpenPax.Sim.Players;
using OpenPax.Ui.Widgets;

namespace OpenPax.Ui;

/// <summary>
/// The offer popup (0x1F, VIEW 43000): a treaty or a trade package offered
/// to the player, with ACCEPT, REJECT and CANCEL. A package lists what is
/// offered and what is asked under captions. The game clock runs while it
/// is open and nothing checks that the offer still stands (a quirk kept).
/// </summary>
public sealed class OfferPopup : IPopup
{
    public const int ViewId = 43000;
    private const int Cancel = 43001;
    private const int Accept = 43002;
    private const int Reject = 43003;
    private const int List = 43005;

    private readonly UiApp _app;
    private readonly GameSession _session;
    private readonly TreatyOfferRow? _treaty;
    private readonly TradePackage? _package;

    /// <summary>A treaty offer.</summary>
    public OfferPopup(UiApp app, GameSession session, TreatyOfferRow treaty)
        : this(app, session)
    {
        _treaty = treaty;
        Window.Get<ListBoxPane>(List).Add(new TreatyOfferRow(treaty.From, treaty.State));
    }

    /// <summary>
    /// A package. The original leaves the treaty fields unset on
    /// this path; the port keeps them empty, which is what the fill expects.
    /// </summary>
    public OfferPopup(UiApp app, GameSession session, TradePackage package)
        : this(app, session)
    {
        _package = package;
        var rows = new List<ListRow>();
        var name = package.Proposer.Name;
        var strings = app.Assets.Strings;
        AddItems(rows, package.Asked);
        if (!package.IsGift)
        {
            rows.Add(Caption(name + strings.Get(new TextRef(83, package.IsDemand ? 0 : 1))));
        }

        AddItems(rows, package.Offered);
        if (!package.IsDemand)
        {
            rows.Add(Caption(name + strings.Get(new TextRef(83, package.IsGift ? 2 : 3))));
        }

        // Each row went in first, so the list reads backwards.
        var list = Window.Get<ListBoxPane>(List);
        for (var i = rows.Count - 1; i >= 0; i--)
        {
            list.Add(rows[i]);
        }
    }

    private OfferPopup(UiApp app, GameSession session)
    {
        _app = app;
        _session = session;

        // Open: where the original puts it on screen; the port centers it.
        var size = ViewReader.Parse(app.Assets.Views.Get(OpenPax.Data.Resources.ResourceType.View, ViewId).Span).Base;
        Window = new ViewWindow(app.Assets, ViewId, app.PaletteIndex, ViewId, null, (app.ClassicWidth - size.Width) / 2, (app.ClassicHeight - size.Height) / 2);
    }

    public ViewWindow Window { get; }

    private static CaptionRow Caption(string text) => new(text, 25, 22);

    private void AddItems(List<ListRow> rows, TradeHalf half)
    {
        var game = _session.Game;
        rows.AddRange(half.Planets.Select(id => new TradeItemRow(game, TradeItemRow.PlanetItem, id)));
        rows.AddRange(half.Techs.Select(id => new TradeItemRow(game, TradeItemRow.TechItem, id)));
        rows.AddRange(half.Ships.Select(id => new TradeItemRow(game, TradeItemRow.ShipItem, id)));
        if (half.Credits > 0)
        {
            rows.Add(new TradeItemRow(game, TradeItemRow.CreditsItem, half.Credits));
        }

        if (half.Map)
        {
            rows.Add(new TradeItemRow(game, TradeItemRow.MapItem, -1));
        }
    }

    public bool Handle(UiMessage message)
    {
        switch (message.Message)
        {
            case Accept or Reject:
                var accept = message.Message == Accept;
                var game = _session.Game;
                if (_treaty is { } t)
                {
                    _app.GameEvents(_session.Send(new TreatyReplyCommand(_session.Local.Index, t.From.Index, t.State, accept ? Treaty.InForce : Treaty.Off)));
                }
                else if (_package is { } p)
                {
                    _app.GameEvents(_session.Send(new TradeAnswerCommand(_session.Local.Index, p.Proposer.Index, TradeTerms.Of(p.Offered), TradeTerms.Of(p.Asked), accept)));
                }

                return true;
            case Cancel:
                return true;
            default:
                return false;
        }
    }

    public bool Key(UiKey key)
    {
        return false;
    }
}
