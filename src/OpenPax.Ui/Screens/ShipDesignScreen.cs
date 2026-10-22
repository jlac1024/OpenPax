using System.Globalization;
using OpenPax.Data.Views;
using OpenPax.Sim;
using OpenPax.Sim.Commands;
using OpenPax.Sim.Players;
using OpenPax.Ui.Widgets;

namespace OpenPax.Ui.Screens;

/// <summary>
/// The Ship Design window (mode 15, VIEW 26000, palette 11). Browsing, the
/// player's designs are shown one at a time with the arrows, to delete or
/// to start a new design from. Editing, the design being made is built up
/// category by category: hull, armor class, drives, sensors, shields,
/// weapons by firing arc, and special systems; AUTO fills it as computer
/// players' designs are filled, and ACCEPT saves it into a free slot.
/// </summary>
public sealed class ShipDesignScreen : Screen
{
    public const int ViewId = 26000;
    public const int PaletteIndex = 11;

    private const int Previous = 26001;
    private const int Next = 26002;
    private const int Delete = 26003;
    private const int NewDesign = 26004;
    private const int Clear = 26005;
    private const int Cancel = 26006;
    private const int Accept = 26007;
    private const int Auto = 26008;
    private const int Add = 26009;
    private const int RemoveButton = 26010;
    private const int Overview = 26011;
    private const int Hull = 26012;
    private const int Star = 26013;
    private const int Tactical = 26014;
    private const int Primary = 26015;
    private const int Point = 26018;
    private const int EcmButton = 26019;
    private const int Targeting = 26020;
    private const int ArmorButton = 26021;
    private const int ShieldButton = 26022;
    private const int Special = 26023;
    private const int SpecialList = 26030;
    private const int ShipPicture = 26032;
    private const int Available = 26033;
    private const int TechStats = 26035;
    private const int NameEdit = 26036;
    private const int TechPicture = 26037;
    private const int SlotsLine = 26038;
    private const int Arcs = 26039;
    private const int BrowseCover = 26040;
    private const int ButtonsCover = 26041;
    private const int OverviewPanel = 26042;
    private const int BuildPoints = 26050;
    private const int Maintenance = 26051;
    private const int Title = 26052;
    private const int ClassLine = 26090;
    private const int SpecialLabel = 26095;
    private const int AvailableActivated = 26133;
    private const int OverviewGroup = 26200;
    private const int FittedGroup = 26201;
    private const int AvailableGroup = 26202;
    private const int HullGroup = 26203;
    private const int ArmorGroup = 26204;
    private const int FirstHull = 26210;
    private const int FirstArmor = 26216;
    private const int Fitted = 26300;
    private const int FittedHeadings = 26302;
    private const int SlotPanel = 26303;
    private const int AvailableHeadings = 26304;
    private const int FittedActivated = 26400;

    private readonly GameSession _session;
    private Design? _shown;
    private int? _draftWanted;
    private int? _savedSlot;
    private int _category = -1;
    private bool _browse;
    private bool _ready;

    public ShipDesignScreen(UiApp app)
        : base(app, new ViewWindow(app.Assets, ViewId, PaletteIndex, ViewId, MakePanel))
    {
        app.Assets.Cursor.Show(MouseCursor.Arrow);
        _session = app.Session ?? throw new InvalidOperationException("The Ship Design window needs a game.");
        var techs = Game.Tables.Tech;
        Window.Get<DesignOverviewPane>(OverviewPanel).TechById = techs;
        Window.Get<TechPicturePane>(TechPicture).FleetColor = Local.FleetColor;

        Window.Get<HeaderBarPane>(AvailableHeadings).Select(_session.DesignAvailableSort);
        Window.Get<HeaderBarPane>(FittedHeadings).Select(_session.DesignFittedSort);
        if (Editing is not { } design)
        {
            SetBrowse(true);
        }
        else
        {
            Window.Get<TextEditPane>(NameEdit).Text = design.Name;
            SetBrowse(false);
            TechPanels();
            SelectCategory(_session.DesignCategory);
            Panels();
            Figures();
        }

        Refresh();
        _ = Window.TakeMessages().ToList();
        _ready = true;
    }

    private static Pane? MakePanel(ViewNode node, int left, int top)
    {
        return node.ClassId switch
        {
            19500 => new WeaponArcsPane(node, left, top),
            26001 => new DesignOverviewPane(node, left, top),
            26002 => new SlotPanelPane(node, left, top),
            18002 => new TechStatsPane(node, left, top),
            9002 => new TechPicturePane(node, left, top),
            _ => null,
        };
    }

    private Player Local => _session.Local;

    private Game Game => _session.Game;

    /// <summary>The design being made, if any (player +0x55C).</summary>
    public Design? Editing => _session.DesignInProgress;

    /// <summary>The existing design shown while browsing (+0x24).</summary>
    public Design? Shown => _shown;

    private ListBoxPane AvailableList => Window.Get<ListBoxPane>(Available);

    private ListBoxPane FittedList => Window.Get<ListBoxPane>(Fitted);

    private static bool IsMount(int category) => category is >= Primary and <= Point;

    private static bool IsList(int category) => IsMount(category) || category == Special;

    /// <summary>
    /// Widens between the DESIGN slot and the grilles, plain metal top and
    /// bottom (docs/ui/widescreen.md): the ship view, the overview and the
    /// design panels' lists grow (their stepped hull buttons all lie left of
    /// it), the button column moves over. The top bar's rail repeats there
    /// rather than stretching, as its columns differ.
    /// </summary>
    public override WideLayout? WideAt { get; } = new([(289, 8)], [(22, 53, 8)]);

    /// <summary>
    /// The shield diagram and the figures beside the view keep their size and
    /// move over with the right half; the maintenance figure stays by its label.
    /// </summary>
    protected override void Widened(int extra, int extraHeight)
    {
        foreach (var (id, by) in ((int, int)[])[(26039, extra), (26035, extra), (26051, 0)])
        {
            var pane = Window.Get(id);
            if (pane.Original is { } o)
            {
                pane.Place(o.Left + by, o.Top, o.Width, o.Height);
            }
        }
    }

    public override void Handle(UiMessage message, long now)
    {
        var id = message.Message;
        switch (id)
        {
            case Previous:
                Step(-1);
                break;
            case Next:
                Step(1);
                break;
            case Delete:
                AskToDelete();
                break;
            case NewDesign:
                StartDesign();
                return;
            case Clear:
                ClearDesign();
                break;
            case Cancel:
                CancelDesign();
                break;
            case Accept:
                AcceptDesign();
                break;
            case Auto:
                AutoDesign();
                Marks();
                break;
            case Add or AvailableActivated:
                AddPicked();
                Marks();
                break;
            case RemoveButton or FittedActivated:
                RemovePicked();
                break;
            case >= Overview and <= Special:
                SelectCategory(id);
                break;
            case Available:
                ComponentPicked(AvailableList.Selected as ComponentRow);
                break;
            case >= FirstHull and < FirstHull + 6 when Editing is { } d:
                // then CLEAR: a new hull loses every component.
                d.SetHull(Game.Tables.Hulls[id - FirstHull]);
                ClearDesign();
                HullLatches();
                break;
            case >= FirstArmor and < FirstArmor + 5 when Editing is { } d:
                d.SetQuality(id - FirstArmor);
                ArmorLatches();
                Marks();
                break;
            case HeaderBarPane.SortChosen when _ready:
                _session.DesignAvailableSort = Window.Get<HeaderBarPane>(AvailableHeadings).Selected;
                _session.DesignFittedSort = Window.Get<HeaderBarPane>(FittedHeadings).Selected;
                return;
            case UiMessage.TextChanged when Editing is { } d:
                d.Name = Window.Get<TextEditPane>(NameEdit).Text;
                break;
            default:
                return;
        }

        Refresh();
    }

    /// <summary>A learned tech joins the list of the category shown (1111); the port refills it.</summary>
    public override void Tick(long now)
    {
        TakeDraft();
        TakeSaved();

        if (Editing is not null && _category is > Hull and not ArmorButton && AvailableList.Rows.Count != Candidates().Count())
        {
            FillAvailable();
        }
    }

    private void SetBrowse(bool browse)
    {
        _browse = browse;
        if (browse)
        {
            GoToSlot(_session.DesignSlot);
            Window.Get(Delete).Enabled = true;
            Window.Get(NewDesign).Enabled = true;
            Window.Get(NameEdit).Enabled = false;
            foreach (var hidden in Enumerable.Range(Clear, 6).Concat(Enumerable.Range(Hull, 12))
                .Concat([AvailableGroup, TechStats, SlotsLine, FittedGroup, SlotPanel, HullGroup, ArmorGroup]))
            {
                Show(hidden, false);
            }

            foreach (var shown in new[] { ShipPicture, BrowseCover, ButtonsCover, Previous, Next, OverviewGroup })
            {
                Show(shown, true);
            }

            Window.Get<WeaponArcsPane>(Arcs).Highlighted = -1;
            return;
        }

        foreach (var hidden in new[] { BrowseCover, ButtonsCover, Previous, Next })
        {
            Show(hidden, false);
        }

        Window.Get(Delete).Enabled = false;
        Window.Get(NewDesign).Enabled = false;
        Window.Get(NameEdit).Enabled = true;
        foreach (var shown in Enumerable.Range(Clear, 6).Concat(Enumerable.Range(Hull, 12)).Append(SlotsLine))
        {
            Show(shown, true);
        }
    }

    /// <summary>Shows the design in a slot, or steps on to the next one.</summary>
    private void GoToSlot(int slot)
    {
        if (slot == (_shown?.Slot ?? -1))
        {
            return;
        }

        _session.DesignSlot = slot;
        _shown = slot >= 0 ? Local.Designs[slot] : null;
        if (_shown is null)
        {
            Step(1);
        }
        else
        {
            ShowDesign();
        }
    }

    /// <summary>
    /// The previous or next design: the nearest
    /// filled slot that way, wrapping round to the remembered one.
    /// </summary>
    private void Step(int direction)
    {
        var designs = Local.Designs;
        var from = _session.DesignSlot;
        var found = -1;
        for (var k = 1; k <= designs.Length; k++)
        {
            var slot = ((from + (direction * k)) % designs.Length + designs.Length) % designs.Length;
            if (designs[slot] is not null)
            {
                found = slot;
                break;
            }
        }

        _session.DesignSlot = found;
        _shown = found >= 0 ? designs[found] : null;
        ShowDesign();
    }

    private void ShowDesign()
    {
        if (_shown is { } shown)
        {
            _session.DesignSlot = shown.Slot;
        }

        Window.Get<WeaponArcsPane>(Arcs).Design = _shown;
        Window.Get<DesignOverviewPane>(OverviewPanel).Design = _shown;
        Window.Get<TextEditPane>(NameEdit).Text = _shown?.Name ?? "";
        Window.Get<PicturePane>(ShipPicture).Picture = _shown is { } d ? HullPicture(d) : 0;
        TechPanels();
        Figures();
    }

    private int HullPicture(Design d) => d.Type + ((Local.FleetColor + 1) * 10);

    /// <summary>
    /// A category picked: its button latched and remembered, its
    /// panels shown and filled, the others hidden.
    /// </summary>
    private void SelectCategory(int id)
    {
        if (id == _category)
        {
            return;
        }

        var old = _category;
        if (old >= Overview)
        {
            Window.Get<ButtonPane>(old).Pressed = false;
        }

        if (IsMount(old))
        {
            Window.Get<WeaponArcsPane>(Arcs).Highlighted = -1;
        }

        _category = id;
        _session.DesignCategory = id;
        Window.Get<ButtonPane>(id).Pressed = true;
        TechPanels();

        void Set(int[] hide, int[] show)
        {
            foreach (var h in hide)
            {
                Show(h, false);
            }

            foreach (var s in show)
            {
                Show(s, true);
            }
        }

        if (id == Overview || Editing is null)
        {
            Set([AvailableGroup, TechStats, SlotPanel, FittedGroup, Add, RemoveButton, HullGroup, ArmorGroup], [OverviewGroup, ShipPicture]);
            Window.Get<PicturePane>(ShipPicture).Picture = Editing is { } d ? HullPicture(d) : 0;
            Figures();
        }
        else if (id == Hull)
        {
            Set([OverviewGroup, FittedGroup, ShipPicture, Add, RemoveButton, AvailableGroup, ArmorGroup], [TechStats, HullGroup]);
            HullLatches();
        }
        else if (id == ArmorButton)
        {
            Set([AvailableGroup, OverviewGroup, FittedGroup, ShipPicture, Add, RemoveButton, HullGroup], [TechStats, ArmorGroup]);
            ArmorLatches();
        }
        else if (!IsList(id))
        {
            Set([OverviewGroup, FittedGroup, ShipPicture, HullGroup, ArmorGroup], [SlotPanel, Add, RemoveButton, AvailableGroup, TechStats]);
            FillAvailable();
            FillSlotPanel();
        }
        else
        {
            Set([SlotPanel, OverviewGroup, ShipPicture, HullGroup, ArmorGroup], [FittedGroup, Add, RemoveButton, AvailableGroup, TechStats]);

            // Moving between the bow and the side arcs keeps the list.
            if (id is >= Primary and < Point && old is >= Primary and < Point)
            {
                ComponentPicked(AvailableList.Selected as ComponentRow);
            }
            else
            {
                FillAvailable();
            }

            if (IsMount(id))
            {
                Window.Get<WeaponArcsPane>(Arcs).Highlighted = id - Primary;
            }

            FillFitted();
        }

        Window.Get<TextLinePane>(Title).Text = App.Assets.Strings.Get(new TextRef(26301, id - Overview));
    }

    /// <summary>The picture and figures panel for the category.</summary>
    private void TechPanels()
    {
        var picture = Window.Get<TechPicturePane>(TechPicture);
        var stats = Window.Get<TechStatsPane>(TechStats);
        if (_shown is null || !_browse)
        {
            if (Editing is not { } d)
            {
                return;
            }

            if (_category is Overview or Hull)
            {
                (picture.Mode, picture.Value) = (2, d.Type);
                (stats.TechId, stats.Extra) = (TechStatsPane.HullText, d.Type);
            }
            else if (_category == ArmorButton)
            {
                (picture.Mode, picture.Value) = (3, d.Quality);
                picture.ArmorPicture = d.Armor?.Tech.Picture ?? 0;
                (stats.TechId, stats.Extra) = (TechStatsPane.ArmorText, d.Quality);
            }
            else
            {
                (picture.Mode, picture.Value) = (2, -1);
                stats.TechId = -1;
            }

            return;
        }

        (picture.Mode, picture.Value) = (2, _shown.Type);
        stats.TechId = -1;
    }

    /// <summary>Which kinds of tech a category lists.</summary>
    private static bool InCategory(int category, TechInfo t)
    {
        return category switch
        {
            Star => t.Kind == 11,
            Tactical => t.Kind == 12,
            >= Primary and < Point => t.Kind is >= 15 and <= 19,
            Point => t.Kind is 15 or 16,
            EcmButton => t.Kind == 14,
            Targeting => t.Kind == 13,
            ShieldButton => t.Kind is >= 20 and <= 22,
            Special => t.Kind is 1 or 2 or 3 or 23,
            _ => false,
        };
    }

    /// <summary>
    /// Known techs of the category no bigger than the whole hull.
    /// Star drives must match the hull's class; of minesweepers only the best.
    /// </summary>
    private IEnumerable<TechInfo> Candidates()
    {
        if (Editing is not { } d)
        {
            return [];
        }

        var bestSweeper = Game.BestOrbital(Local, 3);
        var driveClass = d.Type <= 2 ? 2 : d.Type == 3 ? 3 : 5;
        return Game.Tables.Techs.Where(t => InCategory(_category, t) && Local.Knows(t.Id)
            && TechLevels.Size(t, Local) <= d.Hull.Capacity
            && (_category != Star || t.MaxHullClass == driveClass)
            && (_category != Special || t.Kind != 3 || t.Id == bestSweeper));
    }

    private void FillAvailable()
    {
        var list = AvailableList;
        list.Clear();
        foreach (var tech in Candidates())
        {
            list.Add(ComponentRow.Available(App.Assets, tech, Local));
        }

        if (list.Rows.Count > 0)
        {
            list.Select(list.Rows[0]);
            ComponentPicked((ComponentRow)list.Rows[0]);
        }
    }

    /// <summary>The fitted list for an arc or the specials; the first row picked.</summary>
    private void FillFitted()
    {
        var list = FittedList;
        list.Clear();
        if (Editing is not { } d)
        {
            return;
        }

        IEnumerable<Component> entries = _category == Special
            ? d.Specials.Concat(d.Fighters)
            : d.Weapons.Where(w => w.Mode == _category - Primary);
        foreach (var c in entries)
        {
            list.Add(ComponentRow.FittedRow(App.Assets, c, Local));
        }

        list.Select(list.Rows.Count > 0 ? list.Rows[0] : null);
    }

    private void FillSlotPanel()
    {
        int? slot = _category switch
        {
            Star => 0,
            Tactical => 1,
            EcmButton => 3,
            Targeting => 2,
            ShieldButton => 5,
            _ => null,
        };
        Window.Get<SlotPanelPane>(SlotPanel).TechId = slot is { } s && Editing?.SingleSlot(s) is { } c ? c.Tech.Id : 0;
    }

    /// <summary>A component picked in the list: its picture and figures.</summary>
    private void ComponentPicked(ComponentRow? row)
    {
        if (Editing is null || row is null || _category is < Star or ArmorButton)
        {
            return;
        }

        var picture = Window.Get<TechPicturePane>(TechPicture);
        (picture.Mode, picture.Tech) = (1, row.Tech);
        Window.Get<TechStatsPane>(TechStats).TechId = row.Tech.Id;
    }

    /// <summary>
    /// ADD: the picked component goes in, on the arc of the
    /// category for weapons. Quirk kept: free space is not checked, so a
    /// design can be filled past its hull; it then cannot be accepted.
    /// </summary>
    private void AddPicked()
    {
        if (Editing is not { } d || AvailableList.Selected is not ComponentRow row)
        {
            return;
        }

        if (IsList(_category))
        {
            d.Add(row.Tech, IsMount(_category) ? _category - Primary : Component.NotWeapon);
            FillFitted();
        }
        else
        {
            d.Add(row.Tech, Component.NotWeapon);
            FillSlotPanel();
        }

        Figures();
    }

    /// <summary>REMOVE: one unit of the picked fitted entry, or the single slot's system.</summary>
    private void RemovePicked()
    {
        if (Editing is not { } d)
        {
            return;
        }

        if (IsList(_category) && FittedList.Selected is ComponentRow row)
        {
            d.Remove(row.Tech, IsMount(_category) ? _category - Primary : Component.NotWeapon);
            FillFitted();
        }
        else if (Window.Get<SlotPanelPane>(SlotPanel).TechId is > 0 and var id)
        {
            d.Remove(Game.Tables.Tech(id), Component.NotWeapon);
            FillSlotPanel();
        }

        Figures();
        Marks();
    }

    /// <summary>The hull buttons: the design's latched; those past the best armor's largest hull disabled.</summary>
    private void HullLatches()
    {
        var limit = Local.BestArmor > 0 ? Game.Tables.Tech(Local.BestArmor).ArmorHullLimit : 5;
        for (var k = 0; k < 6; k++)
        {
            var button = Window.Get<ButtonPane>(FirstHull + k);
            button.Pressed = Editing?.Type == k;
            if (k > limit)
            {
                button.Enabled = false;
            }
        }

        TechPanels();
        Figures();
    }

    private void ArmorLatches()
    {
        for (var k = 0; k < 5; k++)
        {
            Window.Get<ButtonPane>(FirstArmor + k).Pressed = Editing?.Quality == k;
        }

        TechPanels();
        Figures();
    }

    /// <summary>DESIGN: a copy of the shown design, or a blank one, named "Unnamed".</summary>
    private void StartDesign()
    {
        var design = Game.StartDesign(Local, _shown);
        design.Name = App.Assets.Strings.Get(new TextRef(100, 3));
        _session.DesignInProgress = design;
        SetBrowse(false);
        Panels();
        _category = -1;
        SelectCategory(Overview);
        Window.Get<TextEditPane>(NameEdit).Text = design.Name;
    }

    /// <summary>CLEAR: a blank design of the same hull with the best armor, armor class NONE.</summary>
    private void ClearDesign()
    {
        if (Editing is not { } d)
        {
            return;
        }

        _session.DesignInProgress = Game.StartDesign(Local, null, d.Type);
        Panels();
    }

    /// <summary>AUTO: the builder fills a blank design of the hull, under the name typed.</summary>
    private void AutoDesign()
    {
        if (Editing is not { } d)
        {
            return;
        }

        // AUTO draws from the game's random numbers, so it is a command; the design comes back as the player's draft.
        _draftWanted = Local.DraftNumber + 1;
        _session.Send(new AutoDesignCommand(Local.Index, d.Type, Window.Get<TextEditPane>(NameEdit).Text));
        TakeDraft();
    }

    /// <summary>
    /// The draft AUTO asked for, once the game has it: at once in single
    /// player, with the next state in a network game. A copy is edited, as
    /// the client's game is overwritten by each state.
    /// </summary>
    private void TakeDraft()
    {
        if (_draftWanted is { } wanted && Local.DraftNumber >= wanted && Local.Draft is { } draft)
        {
            _draftWanted = null;
            _session.DesignInProgress = draft.Copy();
            Panels();
        }
    }

    /// <summary>ACCEPT's design, once its slot holds it: then it is shown and the one in progress dropped (the player's 1006).</summary>
    private void TakeSaved()
    {
        if (_savedSlot is { } slot && Local.Designs[slot] is { } saved)
        {
            _savedSlot = null;
            _session.DesignInProgress = null;
            _shown = saved;
            ShowDesign();
        }
    }

    private void CancelDesign()
    {
        _session.DesignInProgress = null;
        UnlatchCategory();
        ShowDesign();
        SetBrowse(true);
        TechPanels();
    }

    /// <summary>
    /// ACCEPT: the name typed is taken and the design saved
    /// into the first free slot (command 6, op 0), which is then shown.
    /// </summary>
    private void AcceptDesign()
    {
        if (Editing is not { } d)
        {
            return;
        }

        var name = Window.Get<TextEditPane>(NameEdit).Text;
        if (name.Length == 0)
        {
            return;
        }

        d.Name = name;
        UnlatchCategory();
        SetBrowse(true);
        var slot = Array.IndexOf(Local.Designs, null);
        _session.Send(new SaveDesignCommand(Local.Index, DesignSpec.Of(d)));

        // In browse mode the new design is shown once it is in its slot.
        // Quirk kept: with no free slot nothing comes back and the design is
        // lost when the window closes.
        _savedSlot = slot >= 0 ? slot : null;
        TakeSaved();
    }

    private void UnlatchCategory()
    {
        if (_category >= Overview)
        {
            Window.Get<ButtonPane>(_category).Pressed = false;
        }

        _category = -1;
    }

    /// <summary>DELETE: asks, then empties the slot and shows the next design.</summary>
    private void AskToDelete()
    {
        if (_shown is not { } shown)
        {
            return;
        }

        var strings = App.Assets.Strings;
        var question = strings.Get(new TextRef(84, 0)) + strings.Get(new TextRef(84, 12)) + shown.Name + UiText.QuestionEnd;
        App.Confirm(question, () =>
        {
            if (Local.Designs[shown.Slot] == shown)
            {
                _session.Send(new DeleteDesignCommand(Local.Index, shown.Slot));

                // The player's 1007. The original sends none for slot 0 and
                // keeps showing the deleted design; the port steps on anyway.
                Step(1);
                Refresh();
            }
        });
    }

    /// <summary>Every panel from the design being made.</summary>
    private void Panels()
    {
        var d = Editing;
        Window.Get<WeaponArcsPane>(Arcs).Design = d;
        Window.Get<DesignOverviewPane>(OverviewPanel).Design = d;
        if (_category == Overview && d is not null)
        {
            Window.Get<PicturePane>(ShipPicture).Picture = HullPicture(d);
        }
        else if (_category == ArmorButton)
        {
            ArmorLatches();
        }
        else if (IsList(_category))
        {
            FillFitted();
        }
        else
        {
            FillSlotPanel();
        }

        Figures();
        Marks();
    }

    /// <summary>
    /// The figures: special systems, the class line, space used
    /// of the hull's, build points and maintenance, of the design being made
    /// or the one shown.
    /// </summary>
    private void Figures()
    {
        var d = Editing ?? _shown;
        var list = Window.Get<ListBoxPane>(SpecialList);
        list.Clear();
        if (d is not null)
        {
            foreach (var c in d.Specials.Concat(d.Fighters))
            {
                list.Add(ComponentRow.Special(App.Assets, c, Local));
            }
        }

        Window.Get(SpecialLabel).Visible = list.Rows.Count > 0;

        var invariant = CultureInfo.InvariantCulture;
        Window.Get<TextLinePane>(ClassLine).Text = Editing is { } e
            ? Game.Tables.ClassNames[e.Type] + Local.DesignCounters[e.Type].ToString(invariant) + ":"
            : _shown is { } s ? s.ClassName + ":" : "";

        Window.Get<TextLinePane>(SlotsLine).Text = d is null ? "" : d.UsedSpace.ToString(invariant) + "/" + d.Hull.Capacity.ToString(invariant);
        Window.Get<TextLinePane>(BuildPoints).Text = d is null ? "" : d.BuildCost().ToString("#,0", invariant);
        Window.Get<TextLinePane>(Maintenance).Text = d is null ? "" : Sim.Numerics.X87.ToInt(d.Upkeep()).ToString("#,0", invariant);
    }

    /// <summary>Category buttons turn red while their part of the design is empty.</summary>
    private void Marks()
    {
        if (Editing is not { } d)
        {
            return;
        }

        for (var id = Star; id <= Special; id++)
        {
            var filled = id switch
            {
                Star => d.StarDrive is not null,
                Tactical => d.TacticalDrive is not null,
                Targeting => d.Targeting is not null,
                EcmButton => d.Ecm is not null,
                ShieldButton => d.Shield is not null,
                >= Primary and <= Point => d.ArcArmed(id - Primary),
                ArmorButton => d.Quality != 0,
                _ => d.Specials.Count > 0 || d.Fighters.Count > 0,
            };
            var button = Window.Get<ButtonPane>(id);
            button.Picture = filled ? 26003 : 26016;
            button.Color = filled ? (byte)254 : (byte)18;
        }
    }

    /// <summary>
    /// Which buttons work. ACCEPT needs the design to fit its
    /// hull, a tactical drive and a name; nothing else.
    /// </summary>
    private void Refresh()
    {
        var d = Editing;
        var categoryWithParts = _category is not (Overview or Hull or ArmorButton) && _category >= Overview;
        Window.Get(RemoveButton).Enabled = d is not null && categoryWithParts
            && (IsList(_category) ? FittedList.Selected is not null : Window.Get<SlotPanelPane>(SlotPanel).TechId > 0);
        Window.Get(Previous).Enabled = _shown is not null;
        Window.Get(Next).Enabled = _shown is not null;
        Window.Get(Add).Enabled = d is not null && AvailableList.Selected is not null && _category != Overview;
        Window.Get(Accept).Enabled = d is not null && d.UsedSpace <= d.Hull.Capacity && d.TacticalDrive is not null
            && Window.Get<TextEditPane>(NameEdit).Text.Length > 0;
        Window.Get(Delete).Enabled = _browse && _shown is not null && !Local.Eliminated && d is null;
        Window.Get<TextLinePane>(Title).Text = App.Assets.Strings.Get(new TextRef(26301, _category >= Overview ? _category - Overview : 0));
    }

    private void Show(int id, bool shown)
    {
        var pane = Window.Get(id);
        pane.Visible = shown;
        pane.Enabled = shown;
    }
}
