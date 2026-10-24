using OpenPax.Data.Views;
using OpenPax.Sim;
using OpenPax.Sim.Commands;
using OpenPax.Sim.Players;
using OpenPax.Ui.Widgets;

namespace OpenPax.Ui.Screens;

/// <summary>
/// The Game Options window (mode 19, VIEW 32000, palette 12): six icons
/// open its panels: load and save games, the options (surface animation,
/// movies, autosave, game speed, combat scrolling), the volumes, and the
/// planetary profiles (the plan library). NEW GAME and QUIT GAME act at
/// once, with no question. In single player the clock is paused while the
/// window is open. There is no close button: the top bar and the screen
/// keys leave it.
/// </summary>
public sealed class GameOptionsScreen : Screen
{
    public const int ViewId = 32000;
    public const int PaletteIndex = 12;

    private const int LoadIcon = 32001;
    private const int SaveIcon = 32002;
    private const int OptionsIcon = 32003;
    private const int NewGameIcon = 32004;
    private const int QuitIcon = 32005;
    private const int VolumesIcon = 32006;
    private const int FirstIconLabel = 32007;
    private const int PlanName = 32013;
    private const int SaveName = 32014;
    private const int Version = 32015;
    private const int ProfileGroup = 32020;
    private const int PlanList = 32021;
    private const int PreviousPlan = 32023;
    private const int NextPlan = 32024;
    private const int CreatePlan = 32025;
    private const int DeletePlan = 32026;
    private const int PlansDone = 32027;
    private const int VolumesGroup = 32030;
    private const int FirstSlider = 32031;
    private const int LoadGroup = 32040;
    private const int LoadButton = 32042;
    private const int DeleteSave = 32043;
    private const int LoadList = 32044;
    private const int LoadActivated = 32144;
    private const int SaveGroup = 32045;
    private const int SaveButton = 32047;
    private const int SaveList = 32048;
    private const int OptionsGroup = 32050;
    private const int EditProfiles = 32051;
    private const int FirstCheck = 32052;
    private const int FirstSpinner = 32056;
    private const int Highlight = 14;
    private const int Plain = 254;

    /// <summary>The version the window shows.</summary>
    private const string OriginalVersion = "v1.072";

    private static readonly string[] _checks = [Preferences.PlaySurfaceAnim, Preferences.PlayMovies, Preferences.AutoSaveGame];
    private static readonly string[] _spinners = [Preferences.GameSpeed, Preferences.CombatKeyScroll, Preferences.CombatScroll];
    private static readonly int[] _groups = [ProfileGroup, VolumesGroup, LoadGroup, SaveGroup, OptionsGroup];

    private readonly GameSession? _session;
    private readonly bool _paused;
    private int _plan;

    public GameOptionsScreen(UiApp app)
        : base(app, new ViewWindow(app.Assets, ViewId, PaletteIndex, ViewId))
    {
        app.Assets.Cursor.Show(MouseCursor.Arrow);
        _session = app.Session;

        // the preferences into the widgets.
        var prefs = app.Preferences;
        for (var i = 0; i < 3; i++)
        {
            Window.Get<CheckBoxPane>(FirstCheck + i).Set(prefs.Flag(_checks[i]));
            Window.Get<SpinnerPane>(FirstSpinner + i).Value = prefs.Get(_spinners[i]);
            Window.Get<VolumeSliderPane>(FirstSlider + i).Read(prefs);
        }

        // a single player game waits while the window is
        // open. A network game goes on: nobody pauses it, as in the original.
        if (_session is { Clock.Paused: false, Link: null } session)
        {
            session.Clock.Paused = true;
            _paused = true;
        }

        ShowPlan();
        ShowPanel(OptionsIcon);
        Window.Get<TextLinePane>(Version).Text = OriginalVersion;
        Enables();
        _ = Window.TakeMessages().ToList();
    }

    private Player? Local => _session?.Local;

    /// <summary>The panel shown, by its icon (or the profile editor's button).</summary>
    public int Panel { get; private set; }

    /// <summary>The plan library slot the profile editor shows (+0x34).</summary>
    public int PlanSlot => _plan;

    // the clock goes on only if this window stopped it.
    public override void Closed()
    {
        if (_paused && _session is { } session)
        {
            session.Clock.Paused = false;
        }
    }

    private const int WideColumn = 440;

    /// <summary>
    /// Widens on a line of the grid, left of every spinner (docs/ui/widescreen.md):
    /// the grid repeats its cells (the width grows by whole cells), the panels
    /// and the save list grow, the icons right of the line and the spinners
    /// move over.
    /// </summary>
    public override WideLayout? WideAt { get; } = new([(WideColumn, 15)], [(25, 475, 15)], null, 15);

    /// <summary>
    /// Labels across the line keep their size: a panel's title stays over the
    /// gap in its frame, a spinner's heading moves over with it.
    /// </summary>
    protected override void Widened(int extra, int extraHeight)
    {
        foreach (var pane in All(Window.Root))
        {
            if (pane.Node.ClassId == 8025 && pane.Original is { } o && o.Left < WideColumn && o.Left + o.Width > WideColumn)
            {
                pane.Place(o.Left + (o.Left + (o.Width / 2) >= WideColumn ? extra : 0), o.Top, o.Width, o.Height);
            }
        }

        static IEnumerable<Pane> All(Pane pane) => pane.Children.SelectMany(All).Prepend(pane);
    }

    public override void Handle(UiMessage message, long now)
    {
        var id = message.Message;
        switch (id)
        {
            case LoadIcon or SaveIcon or OptionsIcon or VolumesIcon or PlansDone or EditProfiles:
                ShowPanel(id);
                break;
            case NewGameIcon:
                App.NewGame();
                return;
            case QuitIcon:
                App.QuitGame();
                return;
            case UiMessage.Within when message.Value is >= LoadIcon and <= VolumesIcon:
                LabelColor(message.Value + 6, Highlight);
                break;
            case UiMessage.Left:
                // Quirk kept: the GAME OPTIONS label is lit again, whatever panel is open.
                ResetLabels();
                LabelColor(FirstIconLabel + 2, Highlight);
                break;
            case >= FirstCheck and < FirstCheck + 3:
                App.Preferences.Set(_checks[id - FirstCheck], message.Value);
                break;
            case >= FirstSpinner and < FirstSpinner + 3:
                App.Preferences.Set(_spinners[id - FirstSpinner], message.Value);
                break;
            case LoadButton or LoadActivated:
                if (Window.Get<ListBoxPane>(LoadList).Selected is SaveRow toLoad)
                {
                    App.LoadGame(toLoad.Name, now);
                    return;
                }

                break;
            case DeleteSave when Window.Get<ListBoxPane>(LoadList).Selected is SaveRow row:
                AskToDelete(row.Name);
                break;
            case SaveList when Window.Get<ListBoxPane>(SaveList).Selected is SaveRow row:
                Window.Get<TextEditPane>(SaveName).Text = row.Name;
                break;
            case SaveButton:
            case UiMessage.TextDone when message.Value == SaveName:
                Save();
                break;
            case PreviousPlan or NextPlan when Local is { } player:
                _plan = Game.StepPlan(player, _plan, id == NextPlan ? 1 : -1);
                ShowPlan();
                break;
            case CreatePlan when Local is { } player:
                // op 7: a copy of the plan shown in the first empty slot.
                var free = Array.IndexOf(player.Plans, null);
                if (free >= 0)
                {
                    _session!.Send(new CopyPlanCommand(player.Index, _plan, free, App.Assets.Strings.Get(new TextRef(100, 3))));
                    _plan = free;
                    ShowPlan();
                }

                break;
            case DeletePlan when Local is { } player && player.Plans[_plan] is not null:
                AskToDeletePlan(player);
                break;
            case UiMessage.TextChanged when message.Value == PlanName && Local is { } player:
                // op 8: renamed at every key.
                _session!.Send(new RenamePlanCommand(player.Index, _plan, Window.Get<TextEditPane>(PlanName).Text));
                break;
            default:
                break;
        }

        Enables();
    }

    /// <summary>One panel shown, the others hidden, its icon's label lit.</summary>
    private void ShowPanel(int id)
    {
        Panel = id;
        var group = id switch
        {
            LoadIcon => LoadGroup,
            SaveIcon => SaveGroup,
            VolumesIcon => VolumesGroup,
            EditProfiles => ProfileGroup,
            _ => OptionsGroup,
        };
        foreach (var g in _groups)
        {
            Window.Get(g).Visible = g == group;
        }

        Window.Get(SaveName).Visible = group == SaveGroup;
        Window.Get(PlanName).Visible = group == ProfileGroup;
        if (group is LoadGroup or SaveGroup)
        {
            FillSaves();
        }

        // Quirk kept: for DONE and EDIT PLANETARY PROFILES the "label" lit is
        // not a label, so nothing shows lit.
        ResetLabels();
        LabelColor(id + 6, Highlight);
    }

    private void ResetLabels()
    {
        for (var i = 0; i < 6; i++)
        {
            LabelColor(FirstIconLabel + i, Plain);
        }
    }

    private void LabelColor(int id, byte color)
    {
        if (Window.Get(id) is LabelPane label)
        {
            label.Color = color;
        }
    }

    /// <summary>
    /// Both lists from the saved games; the load list's first
    /// is picked. Quirk kept: with no sort key the list reads backwards.
    /// </summary>
    private void FillSaves()
    {
        var names = App.SaveGames?.Names() ?? [];
        foreach (var listId in new[] { LoadList, SaveList })
        {
            var list = Window.Get<ListBoxPane>(listId);
            list.Clear();
            for (var i = names.Count - 1; i >= 0; i--)
            {
                list.Add(new SaveRow(names[i]) { Warning = App.SaveWarning(names[i]), Blocked = App.SaveBlocked(names[i]) });
            }
        }

        var load = Window.Get<ListBoxPane>(LoadList);
        load.Select(load.Rows.Count > 0 ? load.Rows[0] : null);
    }

    /// <summary>SAVE: under the name typed, replacing a save of that name with no question; not during a battle.</summary>
    private void Save()
    {
        var name = Window.Get<TextEditPane>(SaveName).Text;
        if (App.SaveGame(name))
        {
            App.Sound.Play(Audio.SoundIds.GameSaved);
            FillSaves();
            App.Tell(string.Format(System.Globalization.CultureInfo.InvariantCulture, UiText.GameSavedNotice, name));
        }
    }

    private void AskToDelete(string name)
    {
        var strings = App.Assets.Strings;
        var question = strings.Get(new TextRef(84, 0)) + strings.Get(new TextRef(84, 10)) + name + UiText.QuestionEnd;
        App.Confirm(question, () =>
        {
            App.SaveGames?.Delete(name);
            FillSaves();
        });
    }

    private void AskToDeletePlan(Player player)
    {
        var strings = App.Assets.Strings;
        var question = strings.Get(new TextRef(84, 0)) + strings.Get(new TextRef(84, 11)) + UiText.PlanName(App.Assets, player, _plan) + UiText.QuestionEnd;
        App.Confirm(question, () =>
        {
            _session!.Send(new DeletePlanCommand(player.Index, _plan));
            _plan = Game.StepPlan(player, _plan, 1);
            ShowPlan();
            Enables();
        });
    }

    /// <summary>The plan's name and its improvements in order.</summary>
    private void ShowPlan()
    {
        var list = Window.Get<ListBoxPane>(PlanList);
        list.Clear();
        if (Local is not { } player)
        {
            return;
        }

        Window.Get<TextEditPane>(PlanName).Text = player.Plans[_plan] is null ? "" : UiText.PlanName(App.Assets, player, _plan);
        foreach (var id in player.Plans[_plan] ?? [])
        {
            list.Add(new PlanEntryRow(id));
        }

        list.Select(list.Rows.Count > 0 ? list.Rows[0] : null);
    }

    private void Enables()
    {
        var plans = Local?.Plans;
        Window.Get(DeletePlan).Enabled = _plan >= 6 && plans?[_plan] is not null;
        Window.Get(CreatePlan).Enabled = plans is not null && Array.IndexOf(plans, null) >= 0;
        Window.Get(PlanName).Enabled = _plan >= 6;
        var picked = Window.Get<ListBoxPane>(LoadList).Selected is not null;
        Window.Get(LoadButton).Enabled = Window.Get<ListBoxPane>(LoadList).Selected is SaveRow { Blocked: false };
        Window.Get(DeleteSave).Enabled = picked;
        Window.Get(SaveButton).Enabled = Window.Get<TextEditPane>(SaveName).Text.Length > 0 && _session is { Game.InCombat: false };

        // in a network game only the host sets the speed.
        Window.Get(FirstSpinner).Enabled = _session?.Link is not { IsHost: false };
    }
}
