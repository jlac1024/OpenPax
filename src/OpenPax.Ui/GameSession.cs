using OpenPax.Sim;
using OpenPax.Sim.Commands;
using OpenPax.Sim.Galaxy;
using OpenPax.Sim.Players;
using OpenPax.Sim.Time;
using OpenPax.Ui.Network;

namespace OpenPax.Ui;

/// <summary>
/// A game being played: the simulation, the clock that paces it, and the
/// view's own state (the star in view and what is selected, kept on the
/// game object at +0x9C54 and +0x9C58 in the original).
/// </summary>
public sealed class GameSession
{
    /// <param name="local">The player at this screen in a network game; single player plays the first human.</param>
    /// <param name="clock">A host's server clock, so the speed keys pace the server.</param>
    public GameSession(Game game, long now, Player? local = null, GameClock? clock = null)
    {
        Game = game;
        Clock = clock ?? new GameClock(now);
        Local = local ?? game.LocalPlayer ?? game.Players[0];
        CurrentStar = Local.HomeStar;
        Selection = Current;
    }

    /// <summary>A loaded game: the saved view and screen memory come back; the clock is new.</summary>
    public GameSession(SavedSession saved, long now)
        : this(saved.Game, now)
    {
        CurrentStar = saved.CurrentStar;
        Selection = saved.Selection;
        BuildSortColumn = saved.BuildSortColumn;
        LastDesignSlot = saved.LastDesignSlot;
        ResearchField = saved.ResearchField;
        ResearchShowsKnown = saved.ResearchShowsKnown;
        ResearchSortColumn = saved.ResearchSortColumn;
        FleetShowsYards = saved.FleetShowsYards;
        FleetSortColumn = saved.FleetSortColumn;
        ShipSortColumn = saved.ShipSortColumn;
        DesignSortColumn = saved.DesignSortColumn;
        YardSortColumn = saved.YardSortColumn;
        saved.DomesticFilters.AsSpan(0, Math.Min(saved.DomesticFilters.Length, DomesticFilters.Length)).CopyTo(DomesticFilters);
        DomesticSortColumn = saved.DomesticSortColumn;
        ForeignPlayer = saved.ForeignPlayer;
        DesignInProgress = saved.DesignInProgress;
        DesignCategory = saved.DesignCategory;
        DesignSlot = saved.DesignSlot;
        DesignAvailableSort = saved.DesignAvailableSort;
        DesignFittedSort = saved.DesignFittedSort;
    }

    /// <summary>What a save holds of this session.</summary>
    public SavedSession Snapshot(bool cheatsOn = false) => new()
    {
        CheatsOn = cheatsOn,
        Game = Game,
        CurrentStar = CurrentStar,
        Selection = Selection,
        BuildSortColumn = BuildSortColumn,
        LastDesignSlot = LastDesignSlot,
        ResearchField = ResearchField,
        ResearchShowsKnown = ResearchShowsKnown,
        ResearchSortColumn = ResearchSortColumn,
        FleetShowsYards = FleetShowsYards,
        FleetSortColumn = FleetSortColumn,
        ShipSortColumn = ShipSortColumn,
        DesignSortColumn = DesignSortColumn,
        YardSortColumn = YardSortColumn,
        DomesticFilters = [.. DomesticFilters],
        DomesticSortColumn = DomesticSortColumn,
        ForeignPlayer = ForeignPlayer,
        DesignInProgress = DesignInProgress,
        DesignCategory = DesignCategory,
        DesignSlot = DesignSlot,
        DesignAvailableSort = DesignAvailableSort,
        DesignFittedSort = DesignFittedSort,
    };

    public Game Game { get; }

    /// <summary>The network game this session plays in: as host or client. Null in single player.</summary>
    public IGameLink? Link { get; set; }

    /// <summary>Carries out a player's command, or sends it to the server; the events are for the local player's sounds and screens.</summary>
    public StepEvents Send(Command command) => Link is { } link ? link.Send(this, command) : Game.Apply(command) | Game.TakeEvents(Local);

    /// <summary>The local player's notices since the last call.</summary>
    public IReadOnlyList<GameNotice> TakeNotices() => Link is { } link ? link.TakeNotices(this) : Game.TakeNotices(Local);

    public GameClock Clock { get; }

    /// <summary>The player at this screen.</summary>
    public Player Local { get; }

    /// <summary>The star the maps center on and the system view shows (game +0x9C54).</summary>
    public int CurrentStar { get; private set; }

    /// <summary>
    /// What is selected (game +0x9C58): a star, a planet, a fleet or a
    /// wormhole. Null before anything is picked.
    /// </summary>
    public object? Selection { get; private set; }

    /// <summary>Counts selection changes, so screens can tell when to refresh (the player's 1004).</summary>
    public int SelectionVersion { get; private set; }

    public Star Current => Game.Universe.Stars[CurrentStar];

    /// <summary>The build list's sort column, 0 type or 1 name (player +0x530).</summary>
    public int BuildSortColumn { get; set; }

    /// <summary>The design last picked to build, or -1 (player +0x548).</summary>
    public int LastDesignSlot { get; set; } = -1;

    /// <summary>The Research window's field (player +0x538), 0 weapons to 4 space.</summary>
    public int ResearchField { get; set; }

    /// <summary>The Research window lists known techs, not researchable ones (player +0x534).</summary>
    public bool ResearchShowsKnown { get; set; }

    /// <summary>The Research window's sort column, 0 name, 1 type, 2 cost (player +0x53C).</summary>
    public int ResearchSortColumn { get; set; }

    /// <summary>The Fleet Control window's list: false the fleets, true the shipyards (player +0x528).</summary>
    public bool FleetShowsYards { get; set; }

    /// <summary>The Fleet Control window's sort columns, heading indexes (player +0x518 fleets, +0x51C ships, +0x520 designs, +0x524 yards).</summary>
    public int FleetSortColumn { get; set; }

    public int ShipSortColumn { get; set; }

    public int DesignSortColumn { get; set; }

    public int YardSortColumn { get; set; }

    /// <summary>
    /// The Domestic Policy window's display filters: Good, Fair, Bad,
    /// Colonized, Explored (player +0x4FC to +0x500). Nothing in the original
    /// sets them before the first visit; by Jeff's choice the port starts them all on.
    /// </summary>
    public bool[] DomesticFilters { get; } = [true, true, true, true, true];

    /// <summary>The Domestic Policy window's sort caption (player +0x504); the port starts on Planet.</summary>
    public int DomesticSortColumn { get; set; } = 1;

    /// <summary>
    /// The ships of the fleet panel's fleet the player has unselected (ship
    /// +0x50 clear), by ship id: they are split off before the fleet's next
    /// order. Every ship is selected again when the panel shows a fleet.
    /// </summary>
    public HashSet<int> LeftOut { get; } = [];

    /// <summary>The fleet <see cref="LeftOut"/> belongs to.</summary>
    public Sim.Players.Fleet? LeftOutFleet { get; set; }

    /// <summary>Before an order the fleet panel's unselected ships go their own way.</summary>
    public void SplitOff(Sim.Players.Fleet fleet)
    {
        if (fleet == LeftOutFleet && LeftOut.Count > 0)
        {
            Send(new SplitFleetCommand(Local.Index, fleet.ObjectId, [.. LeftOut]));
            LeftOut.Clear();
        }
    }

    /// <summary>The battle window shows its control bar (the player's +0x544); off until first asked for.</summary>
    public bool CombatControls { get; set; }

    /// <summary>
    /// The player picked in the Foreign Affairs window (player +0x540), or
    /// null before any pick. Nothing in the original sets it before the first
    /// visit; the port then picks the first tab, as the original does for the
    /// local player's own index.
    /// </summary>
    public int? ForeignPlayer { get; set; }

    /// <summary>
    /// The design being made in the Ship Design window (player +0x55C), kept
    /// when the window is left in the middle of it; null when none. Open:
    /// whether the original saves it with the game.
    /// </summary>
    public Design? DesignInProgress { get; set; }

    /// <summary>
    /// The Ship Design window's memory: the last category button (+0x508),
    /// the design last shown (+0x50C) and the two lists' sort captions
    /// (+0x510, +0x514). Nothing in the original sets them before the first
    /// visit; the port starts on Overview, the first design, by name.
    /// </summary>
    public int DesignCategory { get; set; } = 26011;

    public int DesignSlot { get; set; }

    public int DesignAvailableSort { get; set; }

    public int DesignFittedSort { get; set; }

    /// <summary>Selects anything, or nothing.</summary>
    public void Select(object? selection)
    {
        Selection = selection;
        SelectionVersion++;
    }

    /// <summary>
    /// Makes a star current. A selection elsewhere is replaced
    /// by the star itself, unless it is a fleet: a fleet stays selected.
    /// </summary>
    public void SetCurrentStar(int star)
    {
        CurrentStar = star;
        var target = Game.Universe.Stars[star];
        if (Selection is not null and not Fleet && !Inside(Selection, target))
        {
            Select(target);
        }
    }

    private static bool Inside(object selection, Star star)
    {
        return selection switch
        {
            Star s => s == star,
            Planet p => star.Planets.Contains(p),
            Wormhole w => w.Star == star.Index,
            Fleet f => f.Star == star.Index,
            _ => false,
        };
    }

    /// <summary>
    /// Polls the clock and runs a tick when one is due (the clock is an
    /// idler, so it runs under every game screen). Null when no
    /// tick ran.
    /// </summary>
    public StepEvents? Tick(long now)
    {
        if (Link is { } link)
        {
            return link.Tick(this, now);
        }

        Clock.InCombat = Game.InCombat;
        return !Game.Over && Clock.Poll(now) ? Game.Step() | Game.TakeEvents(Local) : null;
    }

    /// <summary>The date as the bridge shows it: the year, a dot and tenths.</summary>
    public string Date => Game.Calendar.Year.ToString(System.Globalization.CultureInfo.InvariantCulture)
        + "." + (Game.Calendar.Step / 10).ToString(System.Globalization.CultureInfo.InvariantCulture);
}
