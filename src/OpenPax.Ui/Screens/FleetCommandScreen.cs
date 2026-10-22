using OpenPax.Data.Views;
using OpenPax.Sim.Commands;
using OpenPax.Sim.Galaxy;
using OpenPax.Sim.Players;
using OpenPax.Ui.Widgets;

namespace OpenPax.Ui.Screens;

/// <summary>
/// The Fleet Control window (mode 17, VIEW 20000, palette 5): the player's
/// fleets or shipyards, the ships of the picked fleet or the designs to
/// build, a preview of the picked ship or design, a small galaxy map and
/// the fleet advisor. Ships are scrapped or marked for refit here, and
/// shipyards given ships to build. Splitting, merging and renaming fleets
/// are not in this window.
/// </summary>
public sealed class FleetCommandScreen : Screen
{
    public const int ViewId = 20000;
    public const int PaletteIndex = 5;

    private const int Map = 20001;
    private const int Advisor = 20002;
    private const int Replace = 20003;
    private const int ScrapButton = 20004;
    private const int UpgradeButton = 20005;
    private const int BuildButton = 20006;
    private const int StopButton = 20007;
    private const int FleetListButton = 20008;
    private const int YardListButton = 20009;
    private const int HowMany = 20010;
    private const int YardGroup = 20020;
    private const int YardList = 20021;
    private const int FleetGroup = 20040;
    private const int FleetList = 20041;
    private const int ShipList = 20043;
    private const int ShipHeadings = 20045;
    private const int FleetHeadings = 20046;
    private const int Preview = 20047;
    private const int Title = 20048;
    private const int YardHeadings = 20049;
    private const int DesignHeadings = 20050;
    private const int YardActivated = 20121;
    private const int FleetActivated = 20141;
    private const int ShipActivated = 20143;

    private const int RowFrameTime = 66;

    private readonly GameSession _session;
    private bool _yards;
    private bool _ready;
    private long _lastFrame;
    private string _fleetsKey = "";
    private string _shipsKey = "";
    private string _yardsKey = "";

    public FleetCommandScreen(UiApp app)
        : base(app, new ViewWindow(app.Assets, ViewId, PaletteIndex, ViewId, MakePanel))
    {
        app.Assets.Cursor.Show(MouseCursor.Arrow);
        _session = app.Session ?? throw new InvalidOperationException("The Fleet Control window needs a game.");
        var map = Window.Get<MapPane>(Map);
        map.ViewMode = 0;
        map.Session = _session;
        Window.Get<DesignPreviewPane>(Preview).TechById = _session.Game.Tables.Tech;

        Window.Get<HeaderBarPane>(DesignHeadings).Select(_session.DesignSortColumn);
        Window.Get<HeaderBarPane>(ShipHeadings).Select(_session.ShipSortColumn);
        Window.Get<HeaderBarPane>(FleetHeadings).Select(_session.FleetSortColumn);
        Window.Get<HeaderBarPane>(YardHeadings).Select(_session.YardSortColumn);

        var advisor = Window.Get<AdvisorPortraitPane>(Advisor);
        (advisor.Player, advisor.Role, advisor.Variant) = (_session.Local, 3, 0);

        SetListMode(_session.FleetShowsYards);
        FillFleets();
        FillShips();
        FillYards();
        Refresh();

        // What the fill and the headings said while opening is not heard (the ready byte).
        _ = Window.TakeMessages().ToList();
        _ready = true;
        TakeKeys();
    }

    private static Pane? MakePanel(ViewNode node, int left, int top)
    {
        return node.ClassId switch
        {
            9000 when node.Details is ByteOptionDetails d => new MapPane(node, left, top, d),
            8038 => new DesignPreviewPane(node, left, top),
            19800 => new AdvisorPortraitPane(node, left, top),
            _ => null,
        };
    }

    private Player Local => _session.Local;

    private ListBoxPane Fleets => Window.Get<ListBoxPane>(FleetList);

    private ListBoxPane Yards => Window.Get<ListBoxPane>(YardList);

    private ListBoxPane Ships => Window.Get<ListBoxPane>(ShipList);

    private Fleet? PickedFleet => (Fleets.Selected as FleetRow)?.Fleet;

    private YardRow? PickedYard => Yards.Selected as YardRow;

    /// <summary>
    /// Widens inside the lists' name column, clear of the left oval and the
    /// bottom bar's grille (docs/ui/widescreen.md): the fleet and shipyard
    /// lists give the room to their names, FLEET LIST stretches with its
    /// slot, the galaxy map's frame grows with its rulers tiled, and the
    /// advisor and the fleet's panel move over.
    /// </summary>
    public override WideLayout? WideAt { get; } = new([(70, 25)], [(299, 311, 17), (445, 453, 17)]);

    /// <summary>The galaxy keeps its shape, centered in its widened frame.</summary>
    protected override void Widened(int extra, int extraHeight)
    {
        Window.Get<MapPane>(20001).KeepShape();
    }

    public override void Handle(UiMessage message, long now)
    {
        switch (message.Message)
        {
            case FleetListButton or YardListButton:
                SetListMode(message.Message == YardListButton);
                if (_yards)
                {
                    FillYards();
                }
                else
                {
                    FillFleets();
                }

                FillShips();
                Refresh();
                break;
            case FleetList when !_yards:
                FleetPicked();
                FillShips();
                break;
            case FleetActivated when message.Value >= 0 && message.Value < Fleets.Rows.Count:
                FleetActivatedAt((FleetRow)Fleets.Rows[message.Value]);
                break;
            case YardList when _yards:
                YardPicked();
                break;
            case YardActivated when message.Value >= 0 && message.Value < Yards.Rows.Count:
                PlanetToSector(((YardRow)Yards.Rows[message.Value]).Colony.Planet);
                break;
            case ShipList:
                ShipPicked();
                break;
            case ShipActivated or BuildButton:
                Build();
                break;
            case StopButton:
                Stop();
                break;
            case Replace when !Local.Eliminated:
                App.Open(new GovernorPopup(App, _session, 3));
                break;
            case ScrapButton:
                AskToScrap();
                break;
            case UpgradeButton when PickedFleet is { } fleet:
                _session.Select(fleet);
                App.Open(new UpgradePopup(App, _session));
                break;
            case HeaderBarPane.SortChosen when _ready:
                RememberHeadings(message.Value);
                break;
            case Map when message.Value == MapPane.DoubleStar:
                App.Switch(ScreenMode.SectorDisplay, push: true);
                break;
            default:
                // The spinner's 10002 and the map's order message are not heard.
                break;
        }
    }

    /// <summary>
    /// The list rows' icons step every 66 ms, and the lists follow the game:
    /// the original hears the player's and the game's broadcasts (fleets
    /// added, removed or arriving 1009, 1012, 1013; ships added or removed
    /// 1010, 1011; battles 1016, 1017; work done 1034, 1041, 1046). The port
    /// compares what those would change instead.
    /// </summary>
    public override void Tick(long now)
    {
        if (now - _lastFrame >= RowFrameTime)
        {
            _lastFrame = now;
            foreach (var row in Fleets.Rows.OfType<FleetRow>())
            {
                row.Advance();
            }
        }

        var (fleets, ships, yards) = Keys();
        if (fleets != _fleetsKey)
        {
            FillFleets();
        }
        else if (ships != _shipsKey)
        {
            if (!_yards)
            {
                FillShips();
            }
        }
        else if (yards != _yardsKey)
        {
            Refresh();
        }

        TakeKeys();
    }

    private (string Fleets, string Ships, string Yards) Keys()
    {
        var fleets = string.Join(",", Local.Fleets.Select(f => f.ObjectId + "@" + f.Star));
        var ships = string.Join(",", Local.Fleets.Select(f => (f.InCombat ? "!" : "") + string.Join(".", f.Ships.Select(s => s.ShipId))));
        var yards = string.Join(",", Local.Colonies.Select(c => c.Structures.Count + ":" + string.Join(".", c.Structures.Select(s => s.Count + "/" + s.Queue.Count + "/" + (s.Percent >= 100.0 ? 1 : 0)))));
        return (fleets, ships, yards);
    }

    private void TakeKeys()
    {
        (_fleetsKey, _shipsKey, _yardsKey) = Keys();
    }

    /// <summary>
    /// The headings remembered (1104). Bug kept: the fleet column
    /// is written twice, the second time with the shipyard heading's choice,
    /// and the shipyard choice itself is never kept. A click on the shipyard
    /// headings is not heard at all.
    /// </summary>
    private void RememberHeadings(int code)
    {
        if (code is >= YardRow.SortByName and <= YardRow.SortByProgress)
        {
            return;
        }

        _session.ShipSortColumn = Window.Get<HeaderBarPane>(ShipHeadings).Selected;
        _session.DesignSortColumn = Window.Get<HeaderBarPane>(DesignHeadings).Selected;
        _session.FleetSortColumn = Window.Get<HeaderBarPane>(FleetHeadings).Selected;
        _session.FleetSortColumn = Window.Get<HeaderBarPane>(YardHeadings).Selected;
    }

    /// <summary>Fleets (false) or shipyards (true): the page and its button.</summary>
    private void SetListMode(bool yards)
    {
        _yards = yards;
        _session.FleetShowsYards = yards;
        Window.Get(FleetGroup).Visible = !yards;
        Window.Get(YardGroup).Visible = yards;
        Window.Get<ButtonPane>(FleetListButton).Pressed = !yards;
        Window.Get<ButtonPane>(YardListButton).Pressed = yards;
    }

    /// <summary>
    /// The player's fleets, newest first, then sorted. The
    /// selected fleet's row is picked; with none, the first row is picked and
    /// acted on as if clicked. Quirk kept: when the selection was found the
    /// title stays empty until a row is clicked.
    /// </summary>
    private void FillFleets()
    {
        var want = _session.Selection as Fleet;
        var list = Fleets;
        list.Clear();
        foreach (var fleet in Local.Fleets)
        {
            var row = new FleetRow(fleet);
            list.Add(row);
            if (fleet == want)
            {
                list.Select(row);
            }
        }

        if (list.Selected is null)
        {
            list.Select(list.Rows.Count > 0 ? list.Rows[0] : null);
            if (!_yards)
            {
                FleetPicked();
                FillShips();
            }
        }

        Refresh();
    }

    /// <summary>The player's colonies with a shipyard, picked like the fleets.</summary>
    private void FillYards()
    {
        var want = _session.Selection as Planet;
        var list = Yards;
        list.Clear();
        foreach (var colony in Local.Colonies)
        {
            if (colony.FirstOfKind(TechInfo.ShipyardKind) is { } yard)
            {
                var row = new YardRow(colony, yard);
                list.Add(row);
                if (colony.Planet == want)
                {
                    list.Select(row);
                }
            }
        }

        if (list.Selected is null)
        {
            list.Select(list.Rows.Count > 0 ? list.Rows[0] : null);
            if (_yards)
            {
                YardPicked();
            }
        }
    }

    /// <summary>
    /// The right list: the picked fleet's ships, or every
    /// design. The remembered design is picked; otherwise the first row is
    /// picked and shown.
    /// </summary>
    private void FillShips()
    {
        var list = Ships;
        list.Clear();
        if (!_yards)
        {
            Window.Get<HeaderBarPane>(ShipHeadings).Select(_session.ShipSortColumn);
            foreach (var ship in PickedFleet?.Ships ?? [])
            {
                list.Add(new ShipRow(ship));
            }
        }
        else
        {
            Window.Get<HeaderBarPane>(DesignHeadings).Select(_session.DesignSortColumn);
            foreach (var design in Local.Designs.OfType<Design>())
            {
                var row = new DesignRow(design);
                list.Add(row);
                if (design.Slot == _session.LastDesignSlot)
                {
                    list.Select(row);
                }
            }
        }

        if (list.Selected is null)
        {
            list.Select(list.Rows.Count > 0 ? list.Rows[0] : null);
            ShipPicked();
        }
    }

    /// <summary>
    /// The buttons and preview for the page. The ship list is
    /// made again, so its pick goes back to the first row.
    /// </summary>
    private void Refresh()
    {
        var preview = Window.Get<DesignPreviewPane>(Preview);
        Show(ShipHeadings, !_yards);
        Show(DesignHeadings, _yards);
        FillShips();
        preview.Mode = _yards ? 1 : 0;
        if (Ships.Selected is { } picked)
        {
            preview.Design = Shown(picked);
        }

        if (!_yards)
        {
            var fleet = PickedFleet;
            var gone = Local.Eliminated;
            Window.Get(UpgradeButton).Enabled = !gone && fleet is not null && Sim.Game.AtShipyard(fleet);
            Window.Get(BuildButton).Enabled = false;
            Window.Get(StopButton).Enabled = false;
            Window.Get(HowMany).Enabled = false;

            // The last ship of the last fleet cannot be scrapped from here.
            Window.Get(ScrapButton).Enabled = !gone && fleet is { InCombat: false } && !(Fleets.Rows.Count == 1 && Ships.Rows.Count == 1);
            return;
        }

        Window.Get(UpgradeButton).Enabled = false;
        Window.Get(ScrapButton).Enabled = false;
        YardButtons();
    }

    /// <summary>
    /// BUILD and the count for an idle, whole shipyard, else STOP. Quirk
    /// kept: STOP is offered for a yard only damaged, and there it scraps a
    /// unit of the colony's first damaged structure.
    /// </summary>
    private void YardButtons()
    {
        var yard = PickedYard?.Yard;
        var idle = yard is not null && yard.Queue.Count == 0 && yard.Percent >= 100.0;
        Window.Get(BuildButton).Enabled = idle;
        Window.Get(HowMany).Enabled = idle;
        Window.Get(StopButton).Enabled = yard is not null && !idle;
    }

    private static Design? Shown(ListRow row)
    {
        return row switch
        {
            ShipRow s => s.Ship,
            DesignRow d => d.Design,
            _ => null,
        };
    }

    /// <summary>A fleet picked: its name as title, its star current, the fleet selected, UPGRADE for a fleet at a shipyard.</summary>
    private void FleetPicked()
    {
        if (PickedFleet is not { } fleet)
        {
            return;
        }

        Window.Get<TextLinePane>(Title).Text = fleet.Name;
        _session.SetCurrentStar(fleet.Star);
        _session.Select(fleet);
        Window.Get(UpgradeButton).Enabled = !Local.Eliminated && Sim.Game.AtShipyard(fleet);
    }

    /// <summary>A shipyard picked: the planet's name and " shipyard", the planet selected, the build buttons.</summary>
    private void YardPicked()
    {
        if (PickedYard is not { } row)
        {
            return;
        }

        var planet = row.Colony.Planet;
        Window.Get<TextLinePane>(Title).Text = planet.Name + App.Assets.Strings.Get(new TextRef(82, 0));
        _session.SetCurrentStar(row.Colony.Star);
        _session.Select(planet);
        YardButtons();
    }

    /// <summary>A ship or design picked: the preview shows it; a design is remembered for building.</summary>
    private void ShipPicked()
    {
        var shown = Ships.Selected is { } row ? Shown(row) : null;
        Window.Get<DesignPreviewPane>(Preview).Design = shown;
        if (_yards && shown is not null)
        {
            _session.LastDesignSlot = shown.Slot;
        }
    }

    /// <summary>
    /// A fleet double clicked: out of battle, to the Sector
    /// Display with the fleet selected. A fleet in battle would open the
    /// battle screen (mode 20), which is not ported yet.
    /// </summary>
    private void FleetActivatedAt(FleetRow row)
    {
        // a fleet in battle opens the battle window at its star.
        _session.SetCurrentStar(row.Fleet.Star);
        if (row.Fleet.InCombat)
        {
            App.Switch(ScreenMode.Combat, push: true);
            return;
        }

        _session.Select(row.Fleet);
        App.Switch(ScreenMode.SectorDisplay, push: true);
    }

    private void PlanetToSector(Planet planet)
    {
        _session.SetCurrentStar(_session.Game.Universe.Stars.First(s => s.Planets.Contains(planet)).Index);
        _session.Select(planet);
        App.Switch(ScreenMode.SectorDisplay, push: true);
    }

    /// <summary>BUILD SHIP: on the shipyard page, the count of the picked design at an idle yard.</summary>
    private void Build()
    {
        if (!_yards || PickedYard is not { } yard || yard.Yard.Queue.Count != 0 || Ships.Selected is not DesignRow design)
        {
            return;
        }

        _session.Send(new OrderShipsCommand(Local.Index, yard.Colony.Planet.ObjectId, design.Design.Slot, Window.Get<SpinnerPane>(HowMany).Value));
        Refresh();
    }

    /// <summary>STOP BUILD: the yard's oldest order goes, or a damaged structure's unit.</summary>
    private void Stop()
    {
        if (PickedYard is { } yard)
        {
            _session.Send(new StopWorkCommand(Local.Index, yard.Colony.Planet.ObjectId));
            Refresh();
        }
    }

    /// <summary>SCRAP: asks first, then the ship goes with nothing refunded.</summary>
    private void AskToScrap()
    {
        if (Ships.Selected is not ShipRow row || PickedFleet is not { InCombat: false } fleet || fleet.Owner != Local)
        {
            return;
        }

        var strings = App.Assets.Strings;
        var question = strings.Get(new TextRef(84, 0)) + strings.Get(new TextRef(84, 8)) + row.Ship.Name + UiText.QuestionEnd;
        App.Confirm(question, () =>
        {
            _session.Send(new ScrapShipCommand(Local.Index, row.Ship.ShipId));
            if (App.Current == this && !_yards)
            {
                FillShips();
            }
        });
    }

    private void Show(int id, bool shown)
    {
        var pane = Window.Get(id);
        pane.Visible = shown;
        pane.Enabled = shown;
    }
}
