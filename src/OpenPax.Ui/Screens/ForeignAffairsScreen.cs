using System.Globalization;
using OpenPax.Data.Views;
using OpenPax.Sim;
using OpenPax.Sim.Commands;
using OpenPax.Sim.Numerics;
using OpenPax.Sim.Players;
using OpenPax.Ui.Widgets;

namespace OpenPax.Ui.Screens;

/// <summary>
/// The Foreign Affairs window (mode 18, VIEW 15000, palette 5): a tab per
/// other player, the picked player's standing and figures, the seven
/// treaty boxes toward it, the offers waiting for the player, its spy
/// missions with the espionage totals, and buttons to trade, start a
/// mission, compare empires and replace the foreign advisor.
/// </summary>
public sealed class ForeignAffairsScreen : Screen
{
    public const int ViewId = 15000;
    public const int PaletteIndex = 5;

    private const int SendMessage = 15001;
    private const int TradeButton = 15002;
    private const int Compare = 15003;
    private const int FirstMission = 15004;
    private const int Replace = 15008;
    private const int ViewOffer = 15009;
    private const int Abort = 15010;
    private const int FirstBox = 15012;
    private const int TotalPoints = 15019;
    private const int CounterOps = 15020;
    private const int PlayerName = 15023;
    private const int Status = 15024;
    private const int PlanetCount = 15025;
    private const int OutPicture = 15031;
    private const int Strip = 15032;
    private const int Offers = 15033;
    private const int Missions = 15034;
    private const int Advisor = 15029;
    private const int PeriodTicks = 40;

    // Labels hidden while the picked player is out (end of).
    private static readonly int[] _treatyLabels = [15037, 15038, 15039, 15040, 15053, 15054];

    private readonly GameSession _session;
    private Player? _picked;
    private long _period = -1;
    private string _key = "";

    public ForeignAffairsScreen(UiApp app)
        : base(app, new ViewWindow(app.Assets, ViewId, PaletteIndex, ViewId, MakePanel))
    {
        app.Assets.Cursor.Show(MouseCursor.Arrow);
        _session = app.Session ?? throw new InvalidOperationException("The Foreign Affairs window needs a game.");
        var advisor = Window.Get<AdvisorPortraitPane>(Advisor);
        (advisor.Player, advisor.Role, advisor.Variant) = (Local, 1, 0);
        Window.Get<PicturePane>(OutPicture).KeepShape = true;

        FillStrip();
        FillOffers();
        FillMissions();
        Buttons();
        ShowCounterOps();
        ShowTreaties();
        ShowStats();
        _period = _session.Game.Tick / PeriodTicks;
        _key = Key();
    }

    private static Pane? MakePanel(ViewNode node, int left, int top)
    {
        return node.ClassId == 19800 ? new AdvisorPortraitPane(node, left, top) : null;
    }

    private Player Local => _session.Local;

    private Game Game => _session.Game;

    /// <summary>The player whose tab is picked, if any.</summary>
    public Player? Picked => _picked;

    /// <summary>
    /// Widens at the eighth empire slot's edge (docs/ui/widescreen.md), in
    /// the missions list, the points box and the gap between treaty status
    /// and communications: the slot row repeats a slot across (37 wide), so
    /// the row shows more empires, and the width grows by whole slots; the
    /// lower grid repeats its cells (15 wide). The missions list and ABORT
    /// grow, the advisor and the panels right of the column move over.
    /// </summary>
    public override WideLayout? WideAt { get; } = new([(339, 8)], [(210, 241, 37), (290, 472, 15)], null, 37);

    /// <summary>The info bar's status heading and value move over with the other figures, keeping their size, so the room opens after the empire's name.</summary>
    protected override void Widened(int extra, int extraHeight)
    {
        foreach (var id in (int[])[15041, 15024])
        {
            var pane = Window.Get(id);
            if (pane.Original is { } o)
            {
                pane.Place(o.Left + extra, o.Top, o.Width, o.Height);
            }
        }
    }

    public override void Handle(UiMessage message, long now)
    {
        var id = message.Message;
        switch (id)
        {
            case Strip when Window.Get<ListBoxPane>(Strip).Selected is EmpireTab tab:
                Pick(tab.Player);
                break;
            case Offers or Missions:
                Buttons();
                break;
            case >= FirstBox and < FirstBox + Treaty.States when _picked is { } other:
                App.GameEvents(_session.Send(new TreatyClickCommand(Local.Index, other.Index, id - FirstBox)));
                TreatiesChanged();
                break;
            case >= FirstMission and < FirstMission + 4 when _picked is { } target:
                App.Open(new MissionPopup(App, _session, target, id - FirstMission));
                break;
            case Abort:
                AskToAbort();
                break;
            case ViewOffer:
                ViewPicked();
                break;
            case TradeButton when _picked is { } target:
                App.Open(new TradePopup(App, _session, target));
                break;
            case Replace when !Local.Eliminated:
                App.Open(new GovernorPopup(App, _session, 1));
                break;
            case Compare:
                App.Open(new ComparePopup(App, _session));
                break;
            default:
                // SEND MESSAGE (0x18) is network chat, not ported.
                break;
        }
    }

    /// <summary>
    /// The window follows the game: treaties and packages (the player's 1029
    /// and 1037), missions added, paid or dropped (1025, 1026), and once a
    /// period the figures (1027). The port compares instead of listening.
    /// </summary>
    public override void Tick(long now)
    {
        var key = Key();
        if (key != _key)
        {
            _key = key;
            TreatiesChanged();
            FillMissions();
            Buttons();
        }

        var period = Game.Tick / PeriodTicks;
        if (period != _period)
        {
            _period = period;
            ShowStats();
            ShowCounterOps();
        }
    }

    private string Key()
    {
        var treaties = string.Join(",", Game.Players.Select(p => string.Join("", Enumerable.Range(0, Treaty.States).Select(k => Local.Treaties[p.Index].Slot(k)))));
        var packages = string.Join(",", Local.Packages.Select(p => p.GetHashCode()));
        var missions = string.Join(",", Local.Missions.Select(m => m.Id));
        return treaties + "|" + packages + "|" + missions;
    }

    // cases 1029 and 1037
    private void TreatiesChanged()
    {
        FillOffers();
        Buttons();
        ShowStats();
        ShowTreaties();
    }

    /// <summary>
    /// A tab per other player in index order, the out ones included
    /// . The remembered player's tab is picked; on the first
    /// visit the first tab is picked and acted on.
    /// </summary>
    private void FillStrip()
    {
        var list = Window.Get<ListBoxPane>(Strip);
        list.Clear();
        foreach (var player in Game.Players.Where(p => p != Local))
        {
            list.Add(new EmpireTab(player, Local));
        }

        var remembered = _session.ForeignPlayer;
        if (remembered is null || remembered == Local.Index)
        {
            list.Select(list.Rows.Count > 0 ? list.Rows[0] : null);
            if (list.Selected is EmpireTab first)
            {
                Pick(first.Player);
            }

            return;
        }

        if (list.Rows.OfType<EmpireTab>().FirstOrDefault(t => t.Player.Index == remembered) is { } tab)
        {
            list.Select(tab);
            _picked = tab.Player;
        }
    }

    private void Pick(Player player)
    {
        _session.ForeignPlayer = player.Index;
        _picked = player;
        ShowStats();
        ShowTreaties();
    }

    /// <summary>
    /// Offers waiting for the player: treaties offered to it,
    /// then trade packages. Each row goes in first, so packages read oldest
    /// first, then treaties by player and state, highest first.
    /// </summary>
    private void FillOffers()
    {
        var rows = new List<ListRow>();
        foreach (var other in Game.Players.Where(p => p != Local))
        {
            for (var k = 0; k < Treaty.States; k++)
            {
                if (Local.Treaties[other.Index].Slot(k) == Treaty.OfferedToMe)
                {
                    rows.Add(new TreatyOfferRow(other, k));
                }
            }
        }

        rows.AddRange(Local.Packages.Select(p => new PackageRow(p)));
        var list = Window.Get<ListBoxPane>(Offers);
        list.Clear();
        for (var i = rows.Count - 1; i >= 0; i--)
        {
            list.Add(rows[i]);
        }

        list.Select(list.Rows.Count > 0 ? list.Rows[0] : null);
    }

    /// <summary>The player's missions, oldest (paid first) on top.</summary>
    private void FillMissions()
    {
        var list = Window.Get<ListBoxPane>(Missions);
        var picked = (list.Selected as MissionRow)?.Mission;
        list.Clear();
        foreach (var mission in Local.Missions)
        {
            list.Add(new MissionRow(mission));
        }

        list.Select(list.Rows.OfType<MissionRow>().FirstOrDefault(r => r.Mission == picked) ?? (list.Rows.Count > 0 ? list.Rows[0] : null));
    }

    private void Buttons()
    {
        Window.Get(Abort).Enabled = !Local.Eliminated && Window.Get<ListBoxPane>(Missions).Selected is not null;
        Window.Get(ViewOffer).Enabled = !Local.Eliminated && Window.Get<ListBoxPane>(Offers).Selected is not null;
    }

    private bool Contact => _picked is { } q && !Local.Eliminated && !q.Eliminated && Local.Treaties[q.Index].Level() != Treaty.None;

    /// <summary>The seven boxes from the treaty slots, and the buttons that need contact.</summary>
    private void ShowTreaties()
    {
        var contact = Contact;
        for (var k = 0; k < Treaty.States; k++)
        {
            var box = Window.Get<StateBoxPane>(FirstBox + k);
            var other = contact ? _picked : null;
            box.Enabled = other is not null && Game.TreatyOpen(Local, other, k);
            box.Set(other is null ? Treaty.Off : Local.Treaties[other.Index].Slot(k));
        }

        Window.Get(SendMessage).Enabled = _picked is not { Eliminated: true };
        Window.Get(TradeButton).Enabled = contact;
        for (var c = 0; c < 4; c++)
        {
            Window.Get(FirstMission + c).Enabled = contact;
        }

        var shown = _picked is not { Eliminated: true };
        foreach (var label in _treatyLabels)
        {
            Window.Get(label).Visible = shown;
        }
    }

    /// <summary>
    /// The picked player's line: name and species, standing in
    /// its color, and with contact its colonies, ships, and the research and
    /// trade it gave this year. Counts are exact, with no sight test.
    /// </summary>
    private void ShowStats()
    {
        var q = _picked;
        Window.Get(OutPicture).Visible = q is { Eliminated: true };
        var level = q is null || Local.Eliminated || q.Eliminated ? Treaty.None : Local.Treaties[q.Index].Level();
        var (color, word) = level switch
        {
            Treaty.None => ((byte)255, new TextRef(104, 6)),
            < 1 => ((byte)18, new TextRef(15000, 1)),
            <= 3 => ((byte)16, new TextRef(15000, 2)),
            _ => ((byte)22, new TextRef(15000, 3)),
        };

        var strings = App.Assets.Strings;
        for (var id = PlayerName; id <= PlanetCount + 3; id++)
        {
            Window.Get<TextLinePane>(id).Color = color;
        }

        Window.Get<TextLinePane>(Status).Text = strings.Get(word);
        Window.Get<TextLinePane>(PlayerName).Text = q is null ? "" : q.Name + strings.Get(new TextRef(104, 2)) + q.Species.Name;

        string[] figures = q is null || level == Treaty.None
            ? [.. Enumerable.Repeat(strings.Get(new TextRef(104, 3)), 4)]
            :
            [
                Commas(q.Colonies.Count),
                Commas(q.Fleets.Sum(f => f.Ships.Count)),
                Commas(X87.ToInt(Local.ResearchReceipts[q.Index] * 10.0)),
                strings.Get(new TextRef(104, 0)) + X87.ToInt(Local.TradeReceipts[q.Index] * 10.0).ToString(CultureInfo.InvariantCulture),
            ];
        for (var i = 0; i < 4; i++)
        {
            Window.Get<TextLinePane>(PlanetCount + i).Text = figures[i];
        }
    }

    /// <summary>Espionage points a year, and the defense against spies: doubled for a psychic species.</summary>
    private void ShowCounterOps()
    {
        var e = Local.EspionagePoints * 10.0;
        Window.Get<TextLinePane>(TotalPoints).Text = Commas(X87.ToInt(e));
        var d = Local.Species.Has(Species.Psychic) ? e + e : e;
        Window.Get<TextLinePane>(CounterOps).Text = Commas(X87.ToInt(d)) + App.Assets.Strings.Get(new TextRef(109, 2));
    }

    private static string Commas(int value) => value.ToString("#,0", CultureInfo.InvariantCulture);

    private void AskToAbort()
    {
        if (Window.Get<ListBoxPane>(Missions).Selected is not MissionRow row)
        {
            return;
        }

        var strings = App.Assets.Strings;
        var question = strings.Get(new TextRef(84, 0)) + strings.Get(new TextRef(84, 9)) + MissionRow.Describe(App.Assets, row.Mission) + UiText.QuestionEnd;
        App.Confirm(question, () => _session.Send(new CancelMissionCommand(Local.Index, row.Mission.Id)));
    }

    private void ViewPicked()
    {
        switch (Window.Get<ListBoxPane>(Offers).Selected)
        {
            case TreatyOfferRow treaty:
                App.Open(new OfferPopup(App, _session, treaty));
                break;
            case PackageRow package:
                App.Open(new OfferPopup(App, _session, package.Package));
                break;
            default:
                break;
        }
    }
}
