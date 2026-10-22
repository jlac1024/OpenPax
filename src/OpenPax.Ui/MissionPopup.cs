using System.Globalization;
using OpenPax.Data.Views;
using OpenPax.Sim.Commands;
using OpenPax.Sim.Players;
using OpenPax.Ui.Widgets;

namespace OpenPax.Ui;

/// <summary>
/// The new mission popup (0x1C, VIEW 40000) for one espionage category:
/// two or three missions to pick from, the level (1X, 2X, 4X), what the
/// mission does, its cost in espionage points and its chance. RUN starts
/// it against the picked player; it is paid over time, so nothing is
/// checked against the points.
/// </summary>
public sealed class MissionPopup : IPopup
{
    public const int ViewId = 40000;
    private const int Run = 40001;
    private const int Cancel = 40002;
    private const int FirstSub = 40003;
    private const int FirstLevel = 40006;
    private const int Description = 40009;
    private const int CostLine = 40011;
    private const int Title = 40012;

    // Per category (steal, subvert, sabotage, black ops): each sub button's
    // mission kind and caption entry in STR# 44000; -1 for a blank button.
    private static readonly (int Kind, int Caption)[][] _subs =
    [
        [(SpyMission.StealShips, 3), (SpyMission.StealTechs, 4), (SpyMission.RevealSystems, 10)],
        [(SpyMission.TurnAdvisors, 6), (SpyMission.TurnGovernors, 7), (-1, 2)],
        [(SpyMission.DestroyShips, 3), (SpyMission.Sabotage, 5), (-1, 2)],
        [(SpyMission.LocateColony, 8), (SpyMission.Assassinate, 9), (-1, 2)],
    ];

    // the STR# 80 entry describing each kind.
    private static readonly int[] _descriptions = [1, 6, 7, 8, 4, 5, 9, 3, 2];

    private readonly UiApp _app;
    private readonly GameSession _session;
    private readonly Player _target;
    private readonly int _category;
    private int _sub;
    private int _level;

    /// <param name="category">0 steal, 1 subvert, 2 sabotage, 3 black ops.</param>
    public MissionPopup(UiApp app, GameSession session, Player target, int category)
    {
        app.Assets.Cursor.Show(MouseCursor.Arrow);
        _app = app;
        _session = session;
        _target = target;
        _category = category;

        // Open: where the original puts it on screen; the port centers it.
        var size = ViewReader.Parse(app.Assets.Views.Get(OpenPax.Data.Resources.ResourceType.View, ViewId).Span).Base;
        Window = new ViewWindow(app.Assets, ViewId, app.PaletteIndex, ViewId, null, (app.ClassicWidth - size.Width) / 2, (app.ClassicHeight - size.Height) / 2);

        Window.Get<TextLinePane>(Title).Text = app.Assets.Strings.Get(new TextRef(15000, 38 + category));
        for (var i = 0; i < 3; i++)
        {
            var (kind, caption) = _subs[category][i];
            var button = Window.Get<ButtonPane>(FirstSub + i);
            button.TextIndex = caption;
            button.Enabled = kind >= 0;
        }

        PickLevel(0);
        PickSub(0);
        Window.Get(Run).Enabled = Contact;
    }

    public ViewWindow Window { get; }

    /// <summary>The mission kind picked.</summary>
    public int Kind => _subs[_category][_sub].Kind;

    public int Level => _level;

    private Player Local => _session.Local;

    private bool Contact => !Local.Eliminated && !_target.Eliminated && Local.Treaties[_target.Index].Level() != Treaty.None;

    public bool Handle(UiMessage message)
    {
        switch (message.Message)
        {
            case Run:
                _session.Send(new AddMissionCommand(Local.Index, _target.Index, Kind, _level));
                return true;
            case Cancel:
                return true;
            case >= FirstSub and < FirstSub + 3:
                PickSub(message.Message - FirstSub);
                return false;
            case >= FirstLevel and < FirstLevel + 3:
                PickLevel(message.Message - FirstLevel);
                return false;
            default:
                return false;
        }
    }

    public bool Key(UiKey key)
    {
        return false;
    }

    private void PickSub(int sub)
    {
        _sub = sub;
        for (var i = 0; i < 3; i++)
        {
            Window.Get<ButtonPane>(FirstSub + i).Pressed = i == sub;
        }

        // the description only with contact.
        var list = Window.Get<ListBoxPane>(Description);
        list.Clear();
        if (Contact)
        {
            list.Add(new CaptionRow(_app.Assets.Strings.Get(new TextRef(80, _descriptions[Kind])), 255, 5));
        }

        ShowCost();
    }

    private void PickLevel(int level)
    {
        _level = level;
        for (var i = 0; i < 3; i++)
        {
            Window.Get<ButtonPane>(FirstLevel + i).Pressed = i == level;
        }

        ShowCost();
    }

    /// <summary>"cost (chance%)".</summary>
    private void ShowCost()
    {
        var (cost, chance) = SpyMission.Preview(Kind, _level, Local.Species.Is(Species.Spy));
        var s = _app.Assets.Strings;
        var invariant = CultureInfo.InvariantCulture;
        Window.Get<TextLinePane>(CostLine).Text = cost.ToString(invariant) + s.Get(new TextRef(104, 7)) + chance.ToString(invariant)
            + s.Get(new TextRef(104, 4)) + s.Get(new TextRef(104, 8));
    }
}
