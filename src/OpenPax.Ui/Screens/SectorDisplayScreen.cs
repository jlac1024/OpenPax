using OpenPax.Data.Views;
using OpenPax.Sim.Commands;
using OpenPax.Sim.Galaxy;
using OpenPax.Sim.Players;
using OpenPax.Ui.Widgets;

namespace OpenPax.Ui.Screens;

/// <summary>
/// The Sector Display, the Universe window (VIEW 13000, palette 5): the
/// current star system large, the galaxy small below it, details of the
/// selection on the left, and below right the actions for it. Fleets get
/// orders here, and the player's colonies build ships and orbitals, scrap
/// them, stop work, or are abandoned.
/// </summary>
public sealed class SectorDisplayScreen : Screen
{
    public const int ViewId = 13000;
    public const int PaletteIndex = 5;

    private const int BigMap = 13100;
    private const int SmallMap = 13101;
    private const int Info = 13104;
    private const int Help = 13105;
    private const int ShipsPicture = 13900;
    private const int FleetPanel = 13901;

    private const int PlanetGroup = 13200;
    private const int ExaminePlanet = 13201;
    private const int ColonizePlanet = 13202;
    private const int BuildAShip = 13203;
    private const int Abandon = 13204;
    private const int ScrapOrbital = 13205;
    private const int StopWork = 13206;
    private const int BuildOrbital = 13207;

    private const int FleetGroup = 13220;
    private const int ExamineFleet = 13221;
    private const int MoveButton = 13222;
    private const int AttackButton = 13223;
    private const int InterdictButton = 13224;
    private const int ColonizeButton = 13225;

    private const int OrbitalGroup = 13230;
    private const int FirstOrbital = 13231;
    private const int ScrapGroup = 13240;
    private const int FirstScrap = 13241;
    private const int NothingGroup = 13600;
    private const int EmptyGroup = 13700;
    private const int GovernorGroup = 13800;
    private const int GovernorButton = 13103;
    private const int Fire = 13801;
    private const int GovernorPortrait = 13802;
    private const int FirstGovernorValue = 13803;
    private const int GovernorTitle = 13811;

    private const int BuildGroup = 13920;
    private const int BuildTitle = 13921;
    private const int DesignList = 13922;
    private const int Headings = 13924;
    private const int Preview = 13925;
    private const int HowMany = 13926;
    private const int BuildShip = 13927;
    private const int ListActivated = 14022;

    // The panel states, as the original numbers them.
    private const int NothingState = 0;
    private const int PlanetState = 1;
    private const int FleetState = 2;

    private static readonly int[] _groups = [PlanetGroup, FleetGroup, OrbitalGroup, ScrapGroup, NothingGroup, EmptyGroup, GovernorGroup, BuildGroup];

    // The orbital buttons' kinds, in button order: fighter base, missile
    // base, battle station, mine field, academy, shipyard.
    private static readonly int[] _orbitalKinds = [9, 7, 8, 10, 6, 5];

    // the help line's STR# 13100 entry for each button.
    private static readonly Dictionary<int, int> _help = new()
    {
        [ExaminePlanet] = 0,
        [ColonizePlanet] = 1,
        [Abandon] = 2,
        [BuildAShip] = 3,
        [BuildOrbital] = 4,
        [StopWork] = 5,
        [ScrapOrbital] = 18,
        [ExamineFleet] = 6,
        [MoveButton] = 7,
        [AttackButton] = 8,
        [InterdictButton] = 9,
        [ColonizeButton] = 11,
    };

    // The fields each governor line shows, in line order: domestic, research, espionage, fleet.
    private static readonly int[] _governorFields = [2, 0, 1, 3];

    private int _seenSelection = -1;
    private int _state;

    public SectorDisplayScreen(UiApp app)
        : base(app, new ViewWindow(app.Assets, ViewId, PaletteIndex, ViewId, MakePanel))
    {
        // The big pane shows the system and the small one the galaxy, as the
        // VIEW stores them (swap state 1). The other way round
        // (state 0) needs pictures the game's data does not have, so the
        // port does not offer the swap.
        var system = Window.Get<MapPane>(BigMap);
        system.ViewMode = 1;
        system.BigLook = true;
        system.MadeSky = app.Preferences.Flag(Preferences.MadeSkies);
        system.Session = app.Session;

        var galaxy = Window.Get<MapPane>(SmallMap);
        galaxy.ViewMode = 0;
        galaxy.Session = app.Session;

        Window.Get<InfoPanelPane>(Info).Session = app.Session;
        Window.Get<FleetPanelPane>(FleetPanel).Session = app.Session;
        var preview = Window.Get<DesignPreviewPane>(Preview);
        if (app.Session is { } session)
        {
            preview.TechById = session.Game.Tables.Tech;
        }

        // the list takes the remembered sort column.
        Window.Get<HeaderBarPane>(Headings).Select(app.Session?.BuildSortColumn ?? 0);
        _ = Window.TakeMessages().ToList();
        ShowPanels();
    }

    private static Pane? MakePanel(ViewNode node, int left, int top)
    {
        return node.ClassId switch
        {
            9000 when node.Details is ByteOptionDetails d => new MapPane(node, left, top, d),
            19650 => new InfoPanelPane(node, left, top),
            19800 => new AdvisorPortraitPane(node, left, top),
            13002 => new FleetPanelPane(node, left, top),
            8038 => new DesignPreviewPane(node, left, top),
            _ => null,
        };
    }

    private IEnumerable<MapPane> Maps => [Window.Get<MapPane>(BigMap), Window.Get<MapPane>(SmallMap)];

    private GameSession Session => App.Session ?? throw new InvalidOperationException("The Sector Display needs a game.");

    /// <summary>
    /// Grows across either side of the bottom bar's ornament, between the
    /// galaxy map's notches and clear of the knobs, and down within the
    /// system view and within the galaxy map, each one period of the planet
    /// list's grid, repeated so its lines stay 15 rows apart (docs/ui/widescreen.md). The system view and the galaxy map
    /// grow, the ornament stays centered, the panels right move over. The
    /// galaxy map's rulers repeat at their ticks' spacing.
    /// </summary>
    public override WideLayout? WideAt { get; } = new(
        [(268, 20), (333, 22)],
        [(296, 307, 17), (442, 452, 17)],
        [(245, 15), (426, 15)],
        RepeatRows: true);

    /// <summary>Both maps keep their own shape, as large as fits, centered: the system with its star in the middle of the view, the galaxy in its frame.</summary>
    protected override void Widened(int extra, int extraHeight)
    {
        Window.Get<MapPane>(BigMap).KeepShape();
        Window.Get<MapPane>(SmallMap).KeepShape();
    }

    public override void Handle(UiMessage message, long now)
    {
        var id = message.Message;
        switch (id)
        {
            case UiMessage.Within:
                Window.Get<TextLinePane>(Help).Text = HelpText(message.Value);
                break;
            case UiMessage.Left:
                Window.Get<TextLinePane>(Help).Text = "";
                break;
            case MapPane.OrderHandled:
                SetOrderMode(0);
                break;
            case MoveButton or AttackButton or ColonizeButton:
                SetOrderMode(id == MoveButton ? 1 : id == AttackButton ? 2 : 3);
                break;
            case InterdictButton when Session.Selection is Fleet fleet:
                Session.Send(new FleetOrderCommand(Session.Local.Index, fleet.ObjectId, FleetOrderKind.ToggleIntercept, -1, 0, 0, false));
                SetOrderMode(0);
                break;
            case ColonizePlanet:
                Colonize();
                break;
            case GovernorButton when _state == GovernorGroup:
                ShowPanels();
                break;
            case GovernorButton when !Session.Local.Eliminated:
                ShowState(GovernorGroup);
                break;
            case Fire when Governor() is { } governor:
                // the command that fires a governor; the system gets a new one.
                Session.Send(new FireGovernorCommand(Session.Local.Index, governor.Id));
                ShowGovernor();
                break;
            case BuildAShip:
                ShowBuildPanel();
                break;
            case BuildOrbital:
                ShowState(OrbitalGroup);
                break;
            case ScrapOrbital:
                ShowState(ScrapGroup);
                break;
            case >= FirstOrbital and < FirstOrbital + 6 when Session.Selection is Planet planet:
                // the best known tech of the kind.
                Session.Send(new OrbitalCommand(Session.Local.Index, planet.ObjectId, Session.Game.BestOrbital(Session.Local, _orbitalKinds[id - FirstOrbital])));
                ShowState(_state);
                break;
            case >= FirstScrap and < FirstScrap + 6 when Session.Selection is Planet planet:
                AskToScrap(planet, _orbitalKinds[id - FirstScrap]);
                break;
            case StopWork when Session.Selection is Planet planet:
                Session.Send(new StopWorkCommand(Session.Local.Index, planet.ObjectId));
                ShowState(_state);
                break;
            case Abandon when Session.Selection is Planet planet:
                AskToAbandon(planet);
                break;
            case DesignList:
                PreviewSelected();
                break;
            case HeaderBarPane.SortChosen:
                Session.BuildSortColumn = Window.Get<HeaderBarPane>(Headings).Selected;
                break;
            case BuildShip or ListActivated:
                Build();
                break;
            case ExaminePlanet:
            case BigMap or SmallMap when message.Value == MapPane.DoublePlanet:
                // and: the planet stays selected.
                SetOrderMode(0);
                App.Switch(ScreenMode.PlanetarySurface, push: true);
                break;
            case ExamineFleet:
            case BigMap or SmallMap when message.Value == MapPane.DoubleFleet:
                // and: the fleet stays selected,
                // which is how the Fleet Control window picks it.
                SetOrderMode(0);
                App.Switch(ScreenMode.FleetCommand, push: true);
                break;
            case BigMap or SmallMap when message.Value == MapPane.DoubleMarker:
            case BigMap or SmallMap when message.Value == MapPane.DoubleStar && Session.Game.Battles.Any(b => b.Star == Session.CurrentStar):
                // a star with a battle under way opens the battle window.
                SetOrderMode(0);
                App.Switch(ScreenMode.Combat, push: true);
                break;
            default:
                break;
        }
    }

    public override void Tick(long now)
    {
        if (App.Session is { } session && session.SelectionVersion != _seenSelection)
        {
            ShowPanels();
        }
        else if (_state == GovernorGroup)
        {
            // orig 1003, 1036: the governor's figures follow the game.
            ShowGovernor();
        }
    }

    private void SetOrderMode(int mode)
    {
        foreach (var map in Maps)
        {
            map.OrderMode = mode;
        }
    }

    /// <summary>The help line's text for a button; build and scrap buttons read the planet's stacks.</summary>
    private string HelpText(int button)
    {
        int? entry = button switch
        {
            >= FirstOrbital and < FirstOrbital + 6 => (Stack(_orbitalKinds[button - FirstOrbital]) is null ? 12 : 25) + button - FirstOrbital,
            >= FirstScrap and < FirstScrap + 6 => ((Stack(_orbitalKinds[button - FirstScrap])?.Count ?? 0) > 1 ? 31 : 19) + button - FirstScrap,
            _ => _help.TryGetValue(button, out var e) ? e : null,
        };
        return entry is { } n ? App.Assets.Strings.Get(new TextRef(13100, n)) : "";
    }

    private Structure? Stack(int kind)
    {
        return (Session.Selection as Planet)?.Colony?.FirstOfKind(kind);
    }

    /// <summary>
    /// The planet's Colonize: the first fleet of the player in
    /// that system with a colony ship flies there to found a colony. The
    /// original first splits off any ships unselected in the fleet panel.
    /// </summary>
    private void Colonize()
    {
        if (Session.Selection is not Planet { Colony: null, Class: not 5 } planet)
        {
            return;
        }

        var star = Session.Game.Universe.Stars.First(s => s.Planets.Contains(planet));
        if (star.Fleets.FirstOrDefault(f => f.Owner == Session.Local && Sim.Game.ColonyShips(f) > 0) is { } fleet)
        {
            Session.SplitOff(fleet);
            Session.Send(new FleetOrderCommand(Session.Local.Index, fleet.ObjectId, FleetOrderKind.Colonize, planet.ObjectId, 0, 0, true));
        }
    }

    private void AskToScrap(Planet planet, int kind)
    {
        var strings = App.Assets.Strings;
        var question = strings.Get(new TextRef(84, 0)) + strings.Get(new TextRef(84, Array.IndexOf(_orbitalKinds, kind) + 2));
        App.Confirm(question, () =>
        {
            Session.Send(new ScrapStructureCommand(Session.Local.Index, planet.ObjectId, kind));
            ShowState(_state);
        });
    }

    private void AskToAbandon(Planet planet)
    {
        var strings = App.Assets.Strings;
        var question = strings.Get(new TextRef(84, 0)) + strings.Get(new TextRef(84, 1)) + planet.Name + UiText.QuestionEnd;
        App.Confirm(question, () =>
        {
            App.GameEvents(Session.Send(new AbandonCommand(Session.Local.Index, planet.ObjectId)));
            if (App.Current == this)
            {
                ShowState(PlanetState);
            }
        });
    }

    /// <summary>The build list: every design of the player, sorted; the last one built is picked again.</summary>
    private void ShowBuildPanel()
    {
        ShowState(BuildGroup);
        var list = Window.Get<ListBoxPane>(DesignList);
        list.Clear();
        foreach (var design in Session.Local.Designs.OfType<Design>())
        {
            list.Add(new DesignRow(design));
        }

        // Quirk kept: with no row for the remembered design, the preview keeps what it showed.
        if (list.Rows.OfType<DesignRow>().FirstOrDefault(r => r.Design.Slot == Session.LastDesignSlot) is { } last)
        {
            list.Select(last);
            Window.Get<DesignPreviewPane>(Preview).Design = last.Design;
        }

        Window.Get<TextLinePane>(BuildTitle).Text = (Session.Selection as Planet)?.Name ?? "";
    }

    private void PreviewSelected()
    {
        var row = Window.Get<ListBoxPane>(DesignList).Selected as DesignRow;
        Window.Get<DesignPreviewPane>(Preview).Design = row?.Design;
        Session.LastDesignSlot = row?.Design.Slot ?? Session.LastDesignSlot;
    }

    /// <summary>BUILD SHIP: the spinner's count of the selected design at the planet's shipyard.</summary>
    private void Build()
    {
        if (Window.Get<ListBoxPane>(DesignList).Selected is not DesignRow row || Session.Selection is not Planet planet)
        {
            return;
        }

        Session.Send(new OrderShipsCommand(Session.Local.Index, planet.ObjectId, row.Design.Slot, Window.Get<SpinnerPane>(HowMany).Value));
        ShowState(PlanetState);
    }

    /// <summary>The player's governor of the current system, if any.</summary>
    private Advisor? Governor() => Session.Local.Advisors.FirstOrDefault(a => a.IsGovernor && a.Star == Session.CurrentStar);

    /// <summary>
    /// The governor panel: the current system's governor, its
    /// skill in each field in percent (truncated), and its title.
    /// </summary>
    private void ShowGovernor()
    {
        var portrait = Window.Get<AdvisorPortraitPane>(GovernorPortrait);
        portrait.Player = Session.Local;
        portrait.Role = Advisor.GovernorRole;
        portrait.Star = Session.CurrentStar;
        portrait.Variant = 0;
        var governor = Governor();
        var strings = App.Assets.Strings;
        for (var i = 0; i < _governorFields.Length; i++)
        {
            Window.Get<TextLinePane>(FirstGovernorValue + i).Text = governor is null ? ""
                : ((int)(governor.Value(_governorFields[i]) * 100.0)).ToString(System.Globalization.CultureInfo.InvariantCulture) + strings.Get(new TextRef(109, 2));
        }

        Window.Get<WrappedTextPane>(GovernorTitle).Text = strings.Get(new TextRef(83, 5)) + Session.Current.Name + strings.Get(new TextRef(83, 6));
    }

    /// <summary>The panels for the selection; order modes go back to none.</summary>
    private void ShowPanels()
    {
        _seenSelection = Session.SelectionVersion;
        SetOrderMode(0);
        Window.Get<FleetPanelPane>(FleetPanel).Fleet = Session.Selection as Fleet;
        ShowState(Session.Selection switch
        {
            Planet => PlanetState,
            Fleet => FleetState,
            _ => NothingState,
        });
    }

    /// <summary>Shows one panel state and hides the other groups.</summary>
    private void ShowState(int state)
    {
        _state = state;
        foreach (var id in _groups)
        {
            Show(id, false);
        }

        var details = state is not BuildGroup;
        Show(Info, details);
        Show(Help, details);
        Show(FleetPanel, state == FleetState);
        if (state != FleetState)
        {
            Show(ShipsPicture, false);
        }

        switch (state)
        {
            case PlanetState when Session.Selection is Planet planet:
                ShowPlanetButtons(planet);
                break;
            case FleetState when Session.Selection is Fleet fleet:
                Show(FleetGroup, true);
                var own = fleet.Owner == Session.Local;
                foreach (var id in new[] { ExamineFleet, MoveButton, AttackButton, InterdictButton })
                {
                    Show(id, own);
                }

                Show(ColonizeButton, own && Sim.Game.ColonyShips(fleet) > 0);
                break;
            case OrbitalGroup when (Session.Selection as Planet)?.Colony is { } colony:
                Show(OrbitalGroup, true);
                for (var i = 0; i < _orbitalKinds.Length; i++)
                {
                    Show(FirstOrbital + i, Session.Game.CanBuildOrbital(colony, _orbitalKinds[i]));
                }

                break;
            case ScrapGroup when (Session.Selection as Planet)?.Colony is { } colony:
                Show(ScrapGroup, true);
                for (var i = 0; i < _orbitalKinds.Length; i++)
                {
                    Show(FirstScrap + i, colony.FirstOfKind(_orbitalKinds[i]) is not null);
                }

                break;
            case BuildGroup:
                Show(BuildGroup, true);
                Show(NothingGroup, true);
                break;
            case GovernorGroup:
                // The governor's panel takes the details' place.
                Show(Info, false);
                Show(NothingGroup, true);
                Show(GovernorGroup, true);
                ShowGovernor();
                break;
            default:
                Show(NothingGroup, true);
                break;
        }
    }

    /// <summary>
    /// The planet's buttons: Colonize for a free planet with a colony ship in
    /// the system; for the player's own colony out of battle, Abandon, Build
    /// a Ship with an idle, whole shipyard, Build/Upgrade Orbital with an idle
    /// shipyard queue and something allowed, Stop with a ship queued or a
    /// structure below full health, Scrap with any structure.
    /// </summary>
    private void ShowPlanetButtons(Planet planet)
    {
        Show(PlanetGroup, true);
        var star = Session.Game.Universe.Stars.First(s => s.Planets.Contains(planet));
        Show(ColonizePlanet, planet.Colony is null && Sim.Game.CanColonizeFrom(star, Session.Local));

        var own = planet.Colony is { } c && c.Owner == Session.Local && !planet.InCombat ? c : null;
        var building = own is not null && Sim.Game.ShipInBuild(own);
        var damaged = own is not null && Sim.Game.Damaged(own) is not null;
        Show(Abandon, own is not null);
        Show(BuildAShip, own is not null && !building && !damaged && own.FirstOfKind(TechInfo.ShipyardKind) is not null);
        Show(BuildOrbital, own is not null && !building && _orbitalKinds.Any(k => Session.Game.CanBuildOrbital(own, k)));
        Show(StopWork, own is not null && (building || damaged));
        Show(ScrapOrbital, own is not null && own.Structures.Count > 0);
    }

    /// <summary>Show with enable, or hide with disable.</summary>
    private void Show(int id, bool shown)
    {
        var pane = Window.Get(id);
        pane.Visible = shown;
        pane.Enabled = shown;
    }
}
