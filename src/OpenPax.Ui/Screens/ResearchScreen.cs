using System.Globalization;
using OpenPax.Data.Views;
using OpenPax.Sim;
using OpenPax.Sim.Commands;
using OpenPax.Sim.Players;
using OpenPax.Ui.Widgets;

namespace OpenPax.Ui.Screens;

/// <summary>
/// The Research window (mode 13, VIEW 18000, palette 2): the five research
/// fields' projects with their shares of the research points, a list of
/// the techs of one field (researchable or already known), the shown tech's
/// picture, name and description, and the research advisor. Picking a tech
/// and RESEARCH (or a double click) makes it the field's project, keeping
/// the old project's progress for later.
/// </summary>
public sealed class ResearchScreen : Screen
{
    public const int ViewId = 18000;
    public const int PaletteIndex = 2;

    private const int Fields = 5;
    private const int FirstTab = 18001;
    private const int ResearchButton = 18006;
    private const int Replace = 18007;
    private const int Theoretical = 18008;
    private const int Developed = 18009;
    private const int FirstSpinner = 18020;
    private const int FirstSlot = 18050;
    private const int TotalPoints = 18060;
    private const int Allocated = 18061;
    private const int TechName = 18062;
    private const int Description = 18070;
    private const int Stats = 18071;
    private const int TechList = 18100;
    private const int ListActivated = 18101;
    private const int Picture = 18200;
    private const int Headings = 18303;
    private const int Advisor = 18304;

    private readonly GameSession _session;
    private int _field;
    private bool _showKnown;
    private bool _ready;
    private long _pointsPeriod = -1;
    private int _knownCount;

    public ResearchScreen(UiApp app)
        : base(app, new ViewWindow(app.Assets, ViewId, PaletteIndex, ViewId, MakePanel))
    {
        app.Assets.Cursor.Show(MouseCursor.Arrow);
        _session = app.Session ?? throw new InvalidOperationException("The Research window needs a game.");
        var player = _session.Local;
        var tables = _session.Game.Tables;

        var advisor = Window.Get<AdvisorPortraitPane>(Advisor);
        (advisor.Player, advisor.Role, advisor.Variant) = (player, 0, 1);

        // Quirk kept: each maximum starts at the value plus
        // 100, so the first change after opening is not held to what is left.
        for (var f = 0; f < Fields; f++)
        {
            var spinner = Window.Get<SpinnerPane>(FirstSpinner + f);
            spinner.Value = player.Research[f];
            spinner.Max = spinner.Value + 100;
            spinner.PercentSuffix = true;

            var slot = Window.Get<ResearchSlotPane>(FirstSlot + f);
            slot.Slot = player.ResearchSlots[f];
            slot.TechById = tables.Tech;
        }

        _field = _session.ResearchField;
        _showKnown = _session.ResearchShowsKnown;
        Window.Get<ButtonPane>(Developed).Pressed = _showKnown;
        Window.Get<ButtonPane>(Theoretical).Pressed = !_showKnown;
        Window.Get<HeaderBarPane>(Headings).Select(_session.ResearchSortColumn);
        Refill();
        for (var f = 0; f < Fields; f++)
        {
            Window.Get<ButtonPane>(FirstTab + f).Pressed = f == _field;
        }

        RefreshPoints();
        _knownCount = player.KnownTechs.Count;
        _ready = true;
    }

    private static Pane? MakePanel(ViewNode node, int left, int top)
    {
        return node.ClassId switch
        {
            18001 when node.Details is ByteOptionDetails d => new ResearchSlotPane(node, left, top, d),
            9002 => new TechPicturePane(node, left, top),
            18002 => new TechStatsPane(node, left, top),
            19800 => new AdvisorPortraitPane(node, left, top),
            _ => null,
        };
    }

    private Player Local => _session.Local;

    private ListBoxPane List => Window.Get<ListBoxPane>(TechList);

    /// <summary>
    /// Widens in the gap between the second and third tabs, left of the grid
    /// lines at 175 and 176 (docs/ui/widescreen.md):
    /// the list gives the room to its names, the detail panel grows with it,
    /// the projects column moves over.
    /// </summary>
    public override WideLayout? WideAt { get; } = new(172, 3);

    /// <summary>
    /// The tech's figures stay the size they are, at the right of the detail
    /// panel; its picture sits under the title, in the middle of the panel,
    /// unless that would run into the figures (a window only a little wide).
    /// </summary>
    protected override void Widened(int extra, int extraHeight)
    {
        var figures = Window.Get(18071);
        var picture = Window.Get(18200);
        var title = Window.Get(18062);
        if (figures.Original is { } f && picture.Original is { } p)
        {
            figures.Place(f.Left + extra, f.Top, f.Width, f.Height);
            var middle = title.Left + (title.Width / 2) - (p.Width / 2);
            var clear = f.Left + extra - (f.Left - p.Left - p.Width) - p.Width;
            picture.Place(Math.Max(p.Left, Math.Min(middle, clear)), p.Top, p.Width, p.Height);
        }
    }

    public override void Handle(UiMessage message, long now)
    {
        var id = message.Message;
        switch (id)
        {
            case >= FirstTab and < FirstTab + Fields:
                ChooseField(id - FirstTab);
                break;
            case ResearchButton:
                Research(List.Selected as TechRow);
                break;
            case Replace when !Local.Eliminated:
                App.Open(new GovernorPopup(App, _session, 0));
                break;
            case Theoretical or Developed:
                _showKnown = id == Developed;
                _session.ResearchShowsKnown = _showKnown;
                Window.Get<ButtonPane>(id == Developed ? Theoretical : Developed).Pressed = false;
                Refill();
                break;
            case >= FirstSpinner and < FirstSpinner + Fields:
                Allocate(id - FirstSpinner, message.Value);
                break;
            case >= FirstSlot and < FirstSlot + Fields:
                for (var f = 0; f < Fields; f++)
                {
                    Window.Get<ResearchSlotPane>(FirstSlot + f).Highlighted = FirstSlot + f == id;
                }

                break;
            case ResearchSlotPane.TechClicked:
                JumpToTech(message.Value);
                break;
            case TechList:
                ShowTech((List.Selected as TechRow)?.Tech);
                break;
            case ListActivated when message.Value >= 0 && message.Value < List.Rows.Count:
                Research(List.Rows[message.Value] as TechRow);
                break;
            case HeaderBarPane.SortChosen:
                _session.ResearchSortColumn = Window.Get<HeaderBarPane>(Headings).Selected;
                break;
        }
    }

    /// <summary>
    /// The points and allocation lines follow the research period (the
    /// player's 1027 and 1109), and a learned tech brings its list up to date
    /// (1111; the original adds and removes rows rather than refilling).
    /// </summary>
    public override void Tick(long now)
    {
        var period = _session.Game.Tick / 40;
        if (period != _pointsPeriod)
        {
            _pointsPeriod = period;
            RefreshPoints();
        }

        if (Local.KnownTechs.Count != _knownCount)
        {
            _knownCount = Local.KnownTechs.Count;
            Refill();
        }
    }

    private void ChooseField(int field)
    {
        if (field == _field)
        {
            return;
        }

        Window.Get<ButtonPane>(FirstTab + _field).Pressed = false;
        _field = field;
        _session.ResearchField = field;
        Refill();
    }

    /// <summary>
    /// A spinner let go: every spinner may grow only by what is
    /// left of 100, and the game takes the share, clamped to that too.
    /// Quirk kept: the Allocated line is not brought up to date here.
    /// </summary>
    private void Allocate(int field, int value)
    {
        if (!_ready)
        {
            return;
        }

        var spinners = Enumerable.Range(0, Fields).Select(f => Window.Get<SpinnerPane>(FirstSpinner + f)).ToList();
        var left = 100 - spinners.Sum(s => s.Value);
        foreach (var spinner in spinners)
        {
            spinner.Max = spinner.Value + left;
        }

        _session.Send(new AllocationCommand(Local.Index, field, value));
    }

    /// <summary>
    /// Makes a tech the current field's project: only one that
    /// can be researched and is no field's project yet; the field's old
    /// project stops with its progress kept.
    /// </summary>
    private void Research(TechRow? row)
    {
        if (row is null || Local.Eliminated || !Game.Researchable(Local, row.Tech) || Game.BeingResearched(Local, row.Tech.Id))
        {
            return;
        }

        if (Local.ResearchSlots[_field].Tech != ResearchSlot.None)
        {
            _session.Send(new StopResearchCommand(Local.Index, _field));
        }

        _session.Send(new ResearchCommand(Local.Index, row.Tech.Id, _field));
        RefreshPoints();
        Refill();
    }

    /// <summary>
    /// A project clicked (1112): back to researchable techs if known ones
    /// were listed (without refilling, a quirk kept), the project's field, and
    /// its row picked; then the tech is shown.
    /// </summary>
    private void JumpToTech(int tech)
    {
        if (_showKnown)
        {
            _showKnown = false;
            _session.ResearchShowsKnown = false;
            Window.Get<ButtonPane>(Developed).Pressed = false;
            Window.Get<ButtonPane>(Theoretical).Pressed = true;
        }

        var info = _session.Game.Tables.Tech(tech);
        ChooseField(info.ResearchField);
        Window.Get<ButtonPane>(FirstTab + _field).Pressed = true;
        List.Select(List.Rows.OfType<TechRow>().FirstOrDefault(r => r.Tech.Id == tech));
        ShowTech(info);
    }

    /// <summary>The tech's picture, name and description, and its project framed.</summary>
    private void ShowTech(TechInfo? tech)
    {
        if (tech is null)
        {
            return;
        }

        Window.Get<TechPicturePane>(Picture).Tech = tech;
        Window.Get<TechStatsPane>(Stats).TechId = tech.Id;
        for (var f = 0; f < Fields; f++)
        {
            var slot = Window.Get<ResearchSlotPane>(FirstSlot + f);
            slot.Highlighted = Local.ResearchSlots[f].Tech == tech.Id;
        }

        // ship components and installations
        // describe themselves in the stats panel instead.
        var strings = App.Assets.Strings;
        Window.Get<WrappedTextPane>(Description).Text = tech.Kind is < 1 or > 10 ? strings.Get(new TextRef(100000 + tech.Id, 1)) : "";
        Window.Get<WrappedTextPane>(TechName).Text = strings.Get(new TextRef(100000 + tech.Id, 0));
    }

    /// <summary>
    /// The list again: the field's researchable techs (those
    /// already a project included), or its known ones, in id order then
    /// sorted. The row picked stays picked; else the first is picked and shown.
    /// </summary>
    private void Refill()
    {
        var list = List;
        var picked = (list.Selected as TechRow)?.Tech.Id;
        list.Clear();
        foreach (var tech in _session.Game.Tables.Techs.Where(t => t.ResearchField == _field))
        {
            var known = Local.Knows(tech.Id);
            if (_showKnown ? known : Game.Researchable(Local, tech))
            {
                list.Add(new TechRow(tech, Game.ResearchCostFor(Local, tech), known, Game.BeingResearched(Local, tech.Id)).Named(App.Assets));
            }
        }

        if (list.Rows.OfType<TechRow>().FirstOrDefault(r => r.Tech.Id == picked) is { } same)
        {
            list.Select(same);
        }
        else if (list.Rows.Count > 0)
        {
            list.Select(list.Rows[0]);
            ShowTech(((TechRow)list.Rows[0]).Tech);
        }
    }

    /// <summary>Research points of the period, and the shares given out.</summary>
    private void RefreshPoints()
    {
        var points = Sim.Numerics.X87.ToInt(Local.ResearchPoints * 10.0);
        Window.Get<TextLinePane>(TotalPoints).Text = points.ToString(CultureInfo.InvariantCulture);
        var given = Enumerable.Range(0, Fields).Sum(f => Window.Get<SpinnerPane>(FirstSpinner + f).Value);
        Window.Get<TextLinePane>(Allocated).Text = given.ToString(CultureInfo.InvariantCulture) + App.Assets.Strings.Get(new TextRef(109, 2));
    }
}
