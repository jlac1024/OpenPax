using System.Globalization;
using OpenPax.Data.Views;
using OpenPax.Sim;
using OpenPax.Sim.Commands;
using OpenPax.Sim.Galaxy;
using OpenPax.Sim.Numerics;
using OpenPax.Sim.Players;
using OpenPax.Ui.Widgets;

namespace OpenPax.Ui.Screens;

/// <summary>
/// The Planetary Surface window (mode 14, VIEW 14000, palette 6 and then
/// the planet's class plus 6): a planet's surface with its buildings, its
/// work queue (extendable upward), the colony's plan and construction
/// subsidy, the empire's net income, the colony's figures and its orbitals.
/// The arrows walk the player's colonies. Nothing is built or scrapped here.
/// </summary>
public sealed class PlanetarySurfaceScreen : Screen
{
    public const int ViewId = 14000;
    public const int PaletteIndex = 6;

    private const int Surface = 14001;
    private const int PreviousColony = 14002;
    private const int NextColony = 14003;
    private const int PlanetName = 14004;
    private const int NetIncome = 14013;
    private const int Stats = 14014;
    private const int Subsidy = 14029;
    private const int Extend = 14031;
    private const int PreviousPlan = 14032;
    private const int NextPlan = 14033;
    private const int PlanName = 14034;
    private const int FirstOrbital = 14036;
    private const int QueueGroup = 14100;
    private const int Queue = 14101;
    private const int PeriodTicks = 40;

    // The orbital icons' kinds and pictures, in icon order.
    private static readonly (int Kind, int Picture)[] _orbitals =
    [
        (TechInfo.ShipyardKind, 14034),
        (TechInfo.TrainingBaseKind, 14030),
        (TechInfo.MissileBaseKind, 14033),
        (TechInfo.FighterBaseKind, 14032),
        (TechInfo.BattleStationKind, 14031),
        (TechInfo.MineFieldKind, 14035),
    ];

    private readonly GameSession _session;
    private readonly (int Left, int Top, int Width, int Height) _group;
    private readonly (int Left, int Top, int Width, int Height) _list;
    private Planet? _planet;
    private bool _extended;
    private long _period = -1;
    private string _workKey = "";

    public PlanetarySurfaceScreen(UiApp app)
        : base(app, new ViewWindow(app.Assets, ViewId, PaletteIndex, ViewId, MakePanel))
    {
        _session = app.Session ?? throw new InvalidOperationException("The Planetary Surface window needs a game.");
        var surface = Window.Get<SurfacePane>(Surface);
        surface.Session = _session;
        surface.Animate = app.Preferences.Flag(Preferences.PlaySurfaceAnim);
        Window.Get<ColonyStatsPane>(Stats).Session = _session;
        var group = Window.Get(QueueGroup);
        _group = (group.Left, group.Top, group.Width, group.Height);
        var list = Window.Get(Queue);
        _list = (list.Left, list.Top, list.Width, list.Height);

        ShowPlanet(PlanetOnOpen());
        FillQueue();
        Enables();
        ShowIncome();
        SyncSubsidy();
        ShowOrbitals();
        _period = Game.Tick / PeriodTicks;
        _ = Window.TakeMessages().ToList();
    }

    private static Pane? MakePanel(ViewNode node, int left, int top)
    {
        return node.ClassId switch
        {
            14001 => new SurfacePane(node, left, top),
            14900 => new ColonyStatsPane(node, left, top),
            _ => null,
        };
    }

    private Player Local => _session.Local;

    private Game Game => _session.Game;

    /// <summary>The planet shown.</summary>
    public Planet? Planet => _planet;

    /// <summary>The colony shown, if it is the player's.</summary>
    private Colony? Own => _planet?.Colony is { } c && c.Owner == Local ? c : null;

    /// <summary>
    /// Widens inside the planet's figures block, on a line of its grid
    /// (docs/ui/widescreen.md): the block, its name box and its figures grow,
    /// the grid repeating its cells (the width grows by whole cells); the
    /// orbitals and the middle block stay; the surface above keeps its
    /// landscape centered with reflections to the edges. The gaps between the
    /// blocks were tried first: stretched they streaked, repeated they ribbed.
    /// </summary>
    public override WideLayout? WideAt { get; } = new([(508, 15)], [(362, 478, 15)], null, 15);

    /// <summary>The planet's figures keep their size, centered in the widened block.</summary>
    protected override void Widened(int extra, int extraHeight)
    {
        var figures = Window.Get(14014);
        if (figures.Original is { } o)
        {
            figures.Place(o.Left + (extra / 2), o.Top, o.Width, o.Height);
        }
    }

    public override void Handle(UiMessage message, long now)
    {
        switch (message.Message)
        {
            case PreviousColony or NextColony:
                StepColony(message.Message == NextColony ? 1 : -1);
                break;
            case Subsidy when _planet is { Colony: { } colony } planet && message.Value != colony.Funding:
                // case 14029. Quirk kept: a fractional subsidy snaps to the value shown.
                _session.Send(new FundingCommand(Local.Index, planet.ObjectId, message.Value));
                break;
            case Extend:
                _extended = !_extended;
                Resize();
                break;
            case PreviousPlan or NextPlan when Own is { } colony && _planet is { } planet:
                _session.Send(new ColonyPlanCommand(Local.Index, planet.ObjectId, Game.StepPlan(colony, message.Message == NextPlan ? 1 : -1)));

                // The player's 1034.
                ShowPlanName();
                FillQueue();
                break;
            default:
                break;
        }

        // Every message ends with the income and the enables.
        ShowIncome();
        Enables();
    }

    /// <summary>
    /// The window follows the colony: its work queue when improvements,
    /// structures or ships change (1034, 1040 to 1046, 1111), and each period
    /// the income and subsidy (1027, 1028). The port compares instead of listening.
    /// </summary>
    public override void Tick(long now)
    {
        var key = WorkKey();
        if (key != _workKey)
        {
            FillQueue();
            ShowPlanName();
            Enables();
        }

        var period = Game.Tick / PeriodTicks;
        if (period != _period)
        {
            _period = period;
            SyncSubsidy();
            ShowIncome();
            Enables();
        }
    }

    private string WorkKey()
    {
        if (Own is not { } colony)
        {
            return "";
        }

        var work = string.Join(",", Game.UnfinishedImprovements(colony).Select(i => i.Tech.Id));
        var structures = string.Join(",", colony.Structures.Select(s => s.Tech.Id + ":" + s.Count + ":" + s.Queue.Count + ":" + (s.Percent < 100.0 ? 1 : 0)));
        return colony.Plan + "|" + work + "|" + structures;
    }

    /// <summary>
    /// The planet to open on: the selected planet; else the
    /// current star's first planet that is not a gas giant, if the star has
    /// been seen; else the newest colony; else the first fleet's star.
    /// The original crashes when the star holds only gas giants; the port
    /// then takes the newest colony.
    /// </summary>
    private Planet? PlanetOnOpen()
    {
        if (_session.Selection is Planet selected)
        {
            return selected;
        }

        static Planet? FirstSolid(Star star) => star.Planets.FirstOrDefault(p => p.Class != 5);
        var current = _session.Current;
        Planet? found = null;
        if (current.Sight[Local.Index] != Star.Unseen)
        {
            found = FirstSolid(current);
        }
        else if (Local.Colonies.Count > 0)
        {
            found = Local.Colonies[0].Planet;
        }
        else if (Local.Fleets.Count > 0)
        {
            found = FirstSolid(Game.Universe.Stars[Local.Fleets[0].Star]);
        }
        else
        {
            found = FirstSolid(current);
        }

        return found ?? (Local.Colonies.Count > 0 ? Local.Colonies[0].Planet : null);
    }

    /// <summary>
    /// Shows a planet: it becomes the selection (the current
    /// star is left as it was, a quirk kept), the palette follows its class,
    /// and the surface, name and plan follow it.
    /// </summary>
    private void ShowPlanet(Planet? planet)
    {
        _planet = planet;
        if (planet is null)
        {
            return;
        }

        _session.Select(planet);
        Window.PaletteIndex = planet.Class + 6;
        Window.Get<SurfacePane>(Surface).Planet = planet;
        Window.Get<ColonyStatsPane>(Stats).Planet = planet;
        Window.Get<TextLinePane>(PlanetName).Text = planet.Name;
        ShowPlanName();
    }

    private void ShowPlanName()
    {
        Window.Get<TextLinePane>(PlanName).Text = Own is { } colony ? UiText.PlanName(App.Assets, Local, colony.Plan) : "";
    }

    /// <summary>
    /// The colony arrows: the player's colonies,
    /// newest first, wrapping; from a planet not among them, next is the
    /// newest and previous the oldest.
    /// </summary>
    private void StepColony(int direction)
    {
        var colonies = Local.Colonies;
        if (colonies.Count == 0)
        {
            return;
        }

        var at = _planet?.Colony is { } shown ? colonies.IndexOf(shown) : -1;
        var to = at < 0
            ? (direction > 0 ? 0 : colonies.Count - 1)
            : ((at + direction) % colonies.Count + colonies.Count) % colonies.Count;
        ShowPlanet(colonies[to].Planet);
        FillQueue();
        SyncSubsidy();
        ShowOrbitals();
    }

    /// <summary>
    /// The work queue, own colonies only: the yard's ship or the
    /// structure under repair on top, then the unfinished improvements in plan
    /// order. Collapsed, only the top line shows.
    /// </summary>
    private void FillQueue()
    {
        _workKey = WorkKey();
        var list = Window.Get<ListBoxPane>(Queue);
        list.Clear();
        if (Own is { } colony)
        {
            if (colony.FirstOfKind(TechInfo.ShipyardKind) is { Queue.Count: > 0 } yard)
            {
                list.Add(WorkRow.Shipbuilding(yard));
            }
            else if (Game.Damaged(colony) is { } damaged)
            {
                list.Add(WorkRow.Repairing(damaged));
            }

            foreach (var improvement in Game.UnfinishedImprovements(colony))
            {
                list.Add(WorkRow.Building(improvement));
            }

            list.Select(list.Rows.Count > 0 ? list.Rows[0] : null);
        }

        Resize();
    }

    /// <summary>
    /// The queue grows upward when extended, to its rows' height, at most 308
    /// px; its bottom stays put.
    /// </summary>
    private void Resize()
    {
        var list = Window.Get<ListBoxPane>(Queue);
        var margin = _group.Height - _list.Height;
        var height = _extended && list.Rows.Count >= 2 ? Math.Clamp(list.ContentHeight, 14, 308) + margin : margin + 14;
        var top = _group.Top + _group.Height - height;
        Window.Get(QueueGroup).Place(_group.Left, top, _group.Width, height);
        list.Place(_list.Left, top + (_list.Top - _group.Top), _list.Width, height - margin);
        list.Select(list.Rows.Count > 0 ? list.Rows[0] : null);
    }

    private void Enables()
    {
        var colonies = Local.Colonies;
        var onlyShown = colonies.Count == 1 && colonies[0].Planet == _planet;
        Window.Get(PreviousColony).Enabled = colonies.Count > 0 && !onlyShown;
        Window.Get(NextColony).Enabled = colonies.Count > 0 && !onlyShown;
        var own = Own is not null;
        Window.Get(PreviousPlan).Enabled = own;
        Window.Get(NextPlan).Enabled = own;
        Window.Get(Subsidy).Enabled = own;
        Window.Get(Extend).Enabled = own && Window.Get<ListBoxPane>(Queue).Rows.Count > 1;
    }

    /// <summary>The empire's net income, though the caption says Annual Income.</summary>
    private void ShowIncome()
    {
        if (_planet is null)
        {
            return;
        }

        Window.Get<TextLinePane>(NetIncome).Text = App.Assets.Strings.Get(new TextRef(104, 0))
            + X87.ToInt(Game.NetIncome(Local)).ToString("#,0", CultureInfo.InvariantCulture);
    }

    private void SyncSubsidy()
    {
        Window.Get<SpinnerPane>(Subsidy).Value = Own is { } colony ? X87.ToInt(colony.Funding) : 0;
    }

    /// <summary>
    /// An icon for each kind of orbital the colony has, any owner's, when its
    /// star is in view. Quirk kept: only refreshed when the planet changes.
    /// </summary>
    private void ShowOrbitals()
    {
        var star = _planet is { } p ? Game.Universe.Stars.First(s => s.Planets.Contains(p)) : null;
        var colony = star is not null && star.Sight[Local.Index] == Star.InView ? _planet?.Colony : null;
        for (var i = 0; i < _orbitals.Length; i++)
        {
            var (kind, picture) = _orbitals[i];
            Window.Get<PicturePane>(FirstOrbital + i).Picture = colony?.FirstOfKind(kind) is not null ? picture : 0;
        }
    }
}
