using System.Globalization;
using OpenPax.Data.Views;
using OpenPax.Sim.Commands;
using OpenPax.Sim.Galaxy;
using OpenPax.Sim.Numerics;
using OpenPax.Sim.Players;
using OpenPax.Ui.Widgets;

namespace OpenPax.Ui.Screens;

/// <summary>
/// The Domestic Policy window (mode 16, VIEW 17000, palette 5): the
/// empire's yearly budget with the research, espionage and construction
/// spending percents, a browser of planets one column each, filtered and
/// sorted by the captions beside it, the plan picker for an own colony, a
/// small galaxy map and the domestic advisor.
/// </summary>
public sealed class DomesticPolicyScreen : Screen
{
    public const int ViewId = 17000;
    public const int PaletteIndex = 5;

    private const int Columns = 17001;
    private const int FirstFilter = 17003;
    private const int Filters = 5;
    private const int Captions = 17013;
    private const int Advisor = 17014;
    private const int FirstSpinner = 17015;
    private const int FirstCost = 17023;
    private const int Subsidy = 17026;
    private const int Income = 17027;
    private const int Net = 17028;
    private const int Treasury = 17029;
    private const int FleetCost = 17030;
    private const int Map = 17033;
    private const int Replace = 17038;
    private const int PreviousPlan = 17039;
    private const int NextPlan = 17040;
    private const int ColumnActivated = 17101;

    private const int FrameTime = 66;
    private const int PeriodTicks = 40;

    private readonly GameSession _session;
    private readonly string[] _planNames;
    private long _lastFrame;
    private long _period = -1;
    private string _planetsKey = "";

    public DomesticPolicyScreen(UiApp app)
        : base(app, new ViewWindow(app.Assets, ViewId, PaletteIndex, ViewId, MakePanel))
    {
        app.Assets.Cursor.Show(MouseCursor.Arrow);
        _session = app.Session ?? throw new InvalidOperationException("The Domestic Policy window needs a game.");
        _planNames = [.. Enumerable.Range(0, Player.PlanSlots).Select(i => UiText.PlanName(app.Assets, Local, i))];

        var map = Window.Get<MapPane>(Map);
        map.ViewMode = 0;
        map.Session = _session;

        for (var k = 0; k < 3; k++)
        {
            Window.Get<SpinnerPane>(FirstSpinner + k).PercentSuffix = true;
        }

        SyncSpinners();

        for (var f = 0; f < Filters; f++)
        {
            Window.Get<CheckBoxPane>(FirstFilter + f).Set(_session.DomesticFilters[f]);
        }

        var advisor = Window.Get<AdvisorPortraitPane>(Advisor);
        (advisor.Player, advisor.Role, advisor.Variant) = (Local, 2, 0);
        Window.Get<HeaderBarPane>(Captions).Select(_session.DomesticSortColumn);
        Fill();
        PlanArrows();
        ShowBudget();
        _period = _session.Game.Tick / PeriodTicks;
        _planetsKey = PlanetsKey();
    }

    private static Pane? MakePanel(ViewNode node, int left, int top)
    {
        return node.ClassId switch
        {
            9000 when node.Details is ByteOptionDetails d => new MapPane(node, left, top, d),
            19800 => new AdvisorPortraitPane(node, left, top),
            _ => null,
        };
    }

    private Player Local => _session.Local;

    private ListBoxPane List => Window.Get<ListBoxPane>(Columns);

    private PlanetColumn? Picked => List.Selected as PlanetColumn;

    /// <summary>
    /// Widens at the planet table's column line at 326, between two filter
    /// boxes (docs/ui/widescreen.md): the table's rows repeat a whole planet
    /// column (88 wide) across, so the list shows more planets, and the width
    /// grows by whole columns; the galaxy map's frame grows with its rulers
    /// tiled; the advisor moves over.
    /// </summary>
    public override WideLayout? WideAt { get; } = new([(326, 10)], [(28, 35, 17), (172, 179, 17), (205, 460, 88)], null, 88);

    /// <summary>The galaxy keeps its shape, centered in its widened frame.</summary>
    protected override void Widened(int extra, int extraHeight)
    {
        Window.Get<MapPane>(17033).KeepShape();
    }

    // The budget's rows, each its label, value and spinner panes, and what it is for.
    private static readonly (int[] Panes, string Tip)[] _budgetTips =
    [
        ([17036, Treasury], UiText.TipTreasury),
        ([17034, Income], UiText.TipAnnualIncome),
        ([17019, FirstSpinner, FirstCost], UiText.TipResearchSpending),
        ([17020, FirstSpinner + 1, FirstCost + 1], UiText.TipEspionageSpending),
        ([17021, FirstSpinner + 2, FirstCost + 2], UiText.TipConstructionSpending),
        ([17022, Subsidy], UiText.TipSubsidyCost),
        ([17032, FleetCost], UiText.TipFleetCost),
        ([17035, Net], UiText.TipNetIncome),
        ([17037, FirstFilter, FirstFilter + 1, FirstFilter + 2, FirstFilter + 3, FirstFilter + 4], UiText.TipFilters),
        ([Advisor, Replace], UiText.TipDomesticAdvisor),
        ([PreviousPlan, NextPlan], UiText.TipProject),
    ];

    /// <summary>The port's own: what a budget line, filter or planet row is for. A planet column's figure explains its row.</summary>
    public override string? TooltipAt(int x, int y)
    {
        foreach (var (panes, tip) in _budgetTips)
        {
            if (panes.Any(id => Window.Get(id) is { Shown: true } pane && pane.Inside(x, y)))
            {
                return tip;
            }
        }

        var captions = Window.Get<HeaderBarPane>(Captions);
        var code = captions.Inside(x, y) ? captions.CodeAt(x, y)
            : Window.Get(Columns).Inside(x, y) ? captions.CodeOnRow(y)
            : null;
        return code switch
        {
            100 => UiText.TipEmpire,
            101 => UiText.TipPlanet,
            102 => UiText.TipHabitability,
            103 => UiText.TipWealth,
            104 => UiText.TipPopulation,
            105 => UiText.TipColonyIncome,
            106 => UiText.TipColonyEspionage,
            107 => UiText.TipColonyResearch,
            108 => UiText.TipColonyConstruction,
            109 => UiText.TipColonySubsidy,
            110 => UiText.TipProject,
            _ => null,
        };
    }

    public override void Handle(UiMessage message, long now)
    {
        var id = message.Message;
        switch (id)
        {
            case Columns:
                ColumnPicked();
                break;
            case ColumnActivated:
                ColumnPicked();
                App.Switch(ScreenMode.PlanetarySurface, push: true);
                break;
            case >= FirstFilter and < FirstFilter + Filters:
                // Quirk kept: the plan arrows are not looked at again.
                _session.DomesticFilters[id - FirstFilter] = message.Value != 0;
                Fill();
                break;
            case >= FirstSpinner and < FirstSpinner + 3:
                Spend(id - FirstSpinner, message.Value);
                break;
            case Replace when !Local.Eliminated:
                App.Open(new GovernorPopup(App, _session, 2));
                break;
            case PreviousPlan or NextPlan:
                StepPlan(id == NextPlan ? 1 : -1);
                break;
            case HeaderBarPane.SortChosen:
                _session.DomesticSortColumn = Window.Get<HeaderBarPane>(Captions).Selected;
                break;
            case Map when message.Value == MapPane.DoubleStar:
                App.Switch(ScreenMode.SectorDisplay, push: true);
                break;
            default:
                break;
        }
    }

    /// <summary>
    /// Columns step every 66 ms. Each economy period the budget and the
    /// spinners follow the game (the player's 1027 and 1028); colonies founded
    /// or lost and stars coming into view bring the columns up to date (1030,
    /// 1031, 1032). The port compares instead of listening.
    /// </summary>
    public override void Tick(long now)
    {
        if (now - _lastFrame >= FrameTime)
        {
            _lastFrame = now;
            foreach (var column in List.Rows.OfType<PlanetColumn>())
            {
                column.Advance(column == List.Selected);
            }
        }

        var period = _session.Game.Tick / PeriodTicks;
        if (period != _period)
        {
            _period = period;
            SyncSpinners();
            ShowBudget();
        }

        var key = PlanetsKey();
        if (key != _planetsKey)
        {
            _planetsKey = key;
            Fill();
        }
    }

    private string PlanetsKey()
    {
        var stars = _session.Game.Universe.Stars;
        return string.Join(",", stars.Select(s => s.Sight[Local.Index] + ":" + string.Join(".", s.Planets.Select(p => p.Colony?.Owner.Index ?? -1))));
    }

    /// <summary>
    /// The columns: in galaxy order, every planet but gas
    /// giants in a star the player has seen, that passes a habitability
    /// filter and a status filter: Colonized for the player's own colonies,
    /// Explored for planets with no colony known. A colony in a star not in
    /// view counts as none. The selection's column is picked; otherwise none.
    /// </summary>
    private void Fill()
    {
        var filters = _session.DomesticFilters;
        var list = List;
        list.Clear();
        foreach (var star in _session.Game.Universe.Stars)
        {
            var sight = star.Sight[Local.Index];
            if (sight == Star.Unseen)
            {
                continue;
            }

            foreach (var planet in star.Planets)
            {
                if (planet.Class == 5)
                {
                    continue;
                }

                var h = Colony.Habitability(Local.Species, planet);
                var habitable = (filters[0] && h == 2) || (filters[1] && h == 1) || (filters[2] && h == 0);
                var colony = sight == Star.InView ? planet.Colony : null;
                var status = (filters[4] && colony is null) || (filters[3] && colony?.Owner == Local);
                if (habitable && status)
                {
                    var column = new PlanetColumn(planet, star, Local) { PlanNames = _planNames };
                    list.Add(column);
                    if (planet == _session.Selection)
                    {
                        list.Select(column);
                    }
                }
            }
        }
    }

    /// <summary>A column picked: its star current, the planet selected, the plan arrows.</summary>
    private void ColumnPicked()
    {
        if (Picked is not { } column)
        {
            return;
        }

        _session.SetCurrentStar(column.Star.Index);
        _session.Select(column.Planet);
        PlanArrows();
    }

    /// <summary>The plan arrows work on the player's own colony only.</summary>
    private void PlanArrows()
    {
        var own = Picked?.Planet.Colony?.Owner == Local;
        Window.Get(PreviousPlan).Enabled = own;
        Window.Get(NextPlan).Enabled = own;
    }

    private void StepPlan(int step)
    {
        if (Picked?.Planet is { Colony: { } colony } planet && colony.Owner == Local)
        {
            _session.Send(new ColonyPlanCommand(Local.Index, planet.ObjectId, Sim.Game.StepPlan(colony, step)));
        }
    }

    /// <summary>
    /// A spinner let go: the spending percent changes if it differs (command
    /// 10). The three are independent; there is no shared 100.
    /// </summary>
    private void Spend(int field, int value)
    {
        if (value != Local.Spending[field])
        {
            _session.Send(new SpendingCommand(Local.Index, field, value));
            ShowBudget();
        }
    }

    /// <summary>The spinners from the spending percents.</summary>
    private void SyncSpinners()
    {
        for (var k = 0; k < 3; k++)
        {
            var spinner = Window.Get<SpinnerPane>(FirstSpinner + k);
            var value = X87.ToInt(Local.Spending[k]);
            if (spinner.Value != value)
            {
                spinner.Value = value;
            }
        }
    }

    /// <summary>The ledger: each figure a year's money, cut to whole credits on its own.</summary>
    private void ShowBudget()
    {
        var p = Local;
        Money(Treasury, p.Money);
        Money(Income, Sim.Game.AnnualIncome(p));
        for (var k = 0; k < 3; k++)
        {
            Money(FirstCost + k, Sim.Game.SpendingCost(p, k));
        }

        Money(Subsidy, Sim.Game.SubsidyCost(p));
        Money(FleetCost, p.Upkeep);
        Money(Net, Sim.Game.NetIncome(p));
    }

    private void Money(int id, double value)
    {
        Window.Get<TextLinePane>(id).Text = App.Assets.Strings.Get(new TextRef(104, 0)) + X87.ToInt(value).ToString("#,0", CultureInfo.InvariantCulture);
    }
}
