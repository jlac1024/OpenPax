using OpenPax.Data.Views;
using OpenPax.Sim.Commands;
using OpenPax.Sim.Players;
using OpenPax.Ui.Widgets;

namespace OpenPax.Ui;

/// <summary>
/// The trade popup (0x1D, VIEW 41000): a package for another player, what
/// the player gives and what it asks for. ADD under either list opens the
/// picker page with ships, planets, techs the other lacks, the star map or
/// credits in steps of 1000; SEND offers the package as it is, with no
/// check (the answer tests that each side still holds its part).
/// </summary>
public sealed class TradePopup : IPopup
{
    public const int ViewId = 41000;
    private const int Send = 41201;
    private const int Cancel = 41202;
    private const int AddGiven = 41203;
    private const int RemoveGiven = 41204;
    private const int AddAsked = 41205;
    private const int RemoveAsked = 41206;
    private const int GivenList = 41207;
    private const int AskedList = 41208;
    private const int MainPage = 41200;
    private const int PickerPage = 41300;
    private const int Done = 41301;
    private const int AddPicked = 41302;
    private const int Ships = 41303;
    private const int Planets = 41304;
    private const int Techs = 41305;
    private const int Information = 41306;
    private const int Money = 41307;
    private const int Credits = 41308;
    private const int Picker = 41309;
    private const int GivenActivated = 41407;
    private const int AskedActivated = 41408;
    private const int PickerActivated = 41509;

    private readonly UiApp _app;
    private readonly GameSession _session;
    private readonly Player _target;
    private readonly TradePackage _package;
    private bool _giving;
    private int _category = Ships;

    public TradePopup(UiApp app, GameSession session, Player target)
    {
        _app = app;
        _session = session;
        _target = target;
        _package = new TradePackage(session.Local, target);

        // Open: where the original puts it on screen; the port centers it.
        var size = ViewReader.Parse(app.Assets.Views.Get(OpenPax.Data.Resources.ResourceType.View, ViewId).Span).Base;
        Window = new ViewWindow(app.Assets, ViewId, app.PaletteIndex, ViewId, null, (app.ClassicWidth - size.Width) / 2, (app.ClassicHeight - size.Height) / 2);
        ShowPage(false);
    }

    public ViewWindow Window { get; }

    /// <summary>The package being made.</summary>
    public TradePackage Package => _package;

    private TradeHalf Edited => _giving ? _package.Offered : _package.Asked;

    public bool Handle(UiMessage message)
    {
        var id = message.Message;
        switch (id)
        {
            case Send:
                _app.GameEvents(_session.Send(new TradeOfferCommand(_package.Proposer.Index, _package.Target.Index, TradeTerms.Of(_package.Offered), TradeTerms.Of(_package.Asked))));
                return true;
            case Cancel:
                return true;
            case AddGiven or AddAsked:
                _giving = id == AddGiven;
                ShowPage(true);
                Choose(Ships);
                Window.Get<SpinnerPane>(Credits).Value = Edited.Credits / 1000;
                break;
            case >= Ships and <= Money:
                Choose(id);
                break;
            case AddPicked or PickerActivated:
                AddPick();
                break;
            case Credits:
                Edited.Credits = message.Value * 1000;
                break;
            case Done:
                ShowPage(false);
                FillSide(true);
                FillSide(false);
                break;
            case RemoveGiven or GivenActivated:
                Remove(true);
                break;
            case RemoveAsked or AskedActivated:
                Remove(false);
                break;
            default:
                break;
        }

        return false;
    }

    public bool Key(UiKey key)
    {
        return false;
    }

    private void ShowPage(bool picker)
    {
        Window.Get(MainPage).Visible = !picker;
        Window.Get(PickerPage).Visible = picker;
    }

    /// <summary>A category latched; the spinner only for money.</summary>
    private void Choose(int category)
    {
        _category = category;
        for (var b = Ships; b <= Money; b++)
        {
            Window.Get<ButtonPane>(b).Pressed = b == category;
        }

        Window.Get(Credits).Enabled = category == Money;
        FillPicker();
    }

    /// <summary>
    /// What the giving side has that is not in the half yet:
    /// every ship, every colony, techs the other side lacks, the star map.
    /// Quirk kept: the other player's colonies are all listed, seen or not.
    /// </summary>
    private void FillPicker()
    {
        var game = _session.Game;
        var (giver, other) = _giving ? (_session.Local, _target) : (_target, _session.Local);
        var half = Edited;
        var rows = new List<ListRow>();
        switch (_category)
        {
            case Ships:
                foreach (var ship in giver.Fleets.SelectMany(f => f.Ships).Where(s => !half.Ships.Contains(s.ShipId)))
                {
                    rows.Add(new TradeItemRow(game, TradeItemRow.ShipItem, ship.ShipId));
                }

                break;
            case Planets:
                foreach (var colony in giver.Colonies.Where(c => !half.Planets.Contains(c.Planet.ObjectId)))
                {
                    rows.Add(new TradeItemRow(game, TradeItemRow.PlanetItem, colony.Planet.ObjectId));
                }

                break;
            case Techs:
                foreach (var tech in game.Tables.Techs.Where(t => giver.Knows(t.Id) && !other.Knows(t.Id) && !half.Techs.Contains(t.Id)))
                {
                    rows.Add(new TradeItemRow(game, TradeItemRow.TechItem, tech.Id));
                }

                break;
            case Information:
                rows.Add(new TradeItemRow(game, TradeItemRow.MapItem, -1));
                break;
            default:
                break;
        }

        Fill(Window.Get<ListBoxPane>(Picker), rows);
    }

    /// <summary>Each row goes in first, so the lists read backwards; the first shown is picked.</summary>
    private static void Fill(ListBoxPane list, List<ListRow> rows)
    {
        list.Clear();
        for (var i = rows.Count - 1; i >= 0; i--)
        {
            list.Add(rows[i]);
        }

        list.Select(list.Rows.Count > 0 ? list.Rows[0] : null);
    }

    /// <summary>The picked thing goes into the half being made, once.</summary>
    private void AddPick()
    {
        if (Window.Get<ListBoxPane>(Picker).Selected is not TradeItemRow row)
        {
            return;
        }

        var half = Edited;
        switch (row.Kind)
        {
            case TradeItemRow.ShipItem when !half.Ships.Contains(row.Value):
                half.Ships.Add(row.Value);
                break;
            case TradeItemRow.PlanetItem when !half.Planets.Contains(row.Value):
                half.Planets.Add(row.Value);
                break;
            case TradeItemRow.TechItem when !half.Techs.Contains(row.Value):
                half.Techs.Add(row.Value);
                break;
            case TradeItemRow.MapItem:
                half.Map = true;
                break;
            default:
                break;
        }

        FillPicker();
    }

    /// <summary>One side's list ( given asked): planets, techs, ships, credits, the map.</summary>
    private void FillSide(bool given)
    {
        var game = _session.Game;
        var half = given ? _package.Offered : _package.Asked;
        var rows = new List<ListRow>();
        rows.AddRange(half.Planets.Select(v => new TradeItemRow(game, TradeItemRow.PlanetItem, v)));
        rows.AddRange(half.Techs.Select(v => new TradeItemRow(game, TradeItemRow.TechItem, v)));
        rows.AddRange(half.Ships.Select(v => new TradeItemRow(game, TradeItemRow.ShipItem, v)));
        if (half.Credits > 0)
        {
            rows.Add(new TradeItemRow(game, TradeItemRow.CreditsItem, half.Credits));
        }

        if (half.Map)
        {
            rows.Add(new TradeItemRow(game, TradeItemRow.MapItem, -1));
        }

        Fill(Window.Get<ListBoxPane>(given ? GivenList : AskedList), rows);
    }

    /// <summary>The picked line leaves its half.</summary>
    private void Remove(bool given)
    {
        var half = given ? _package.Offered : _package.Asked;
        if (Window.Get<ListBoxPane>(given ? GivenList : AskedList).Selected is not TradeItemRow row)
        {
            return;
        }

        switch (row.Kind)
        {
            case TradeItemRow.PlanetItem:
                half.Planets.Remove(row.Value);
                break;
            case TradeItemRow.TechItem:
                half.Techs.Remove(row.Value);
                break;
            case TradeItemRow.ShipItem:
                half.Ships.Remove(row.Value);
                break;
            case TradeItemRow.CreditsItem:
                half.Credits = 0;
                break;
            default:
                half.Map = false;
                break;
        }

        FillSide(given);
    }
}
