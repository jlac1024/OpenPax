using OpenPax.Data.Views;
using OpenPax.Sim.Combat;
using OpenPax.Sim.Commands;
using OpenPax.Ui.Battle;
using OpenPax.Ui.Widgets;

namespace OpenPax.Ui.Screens;

/// <summary>
/// The battle window (mode 20, VIEW 24000, palette 5): the battle field,
/// its minimap and control bar, the command buttons in pages, AUTO and the
/// flags of the sides. It shows the first battle under way; the game does
/// not open it by itself. When the battle ends it goes to the Sector Display.
/// </summary>
public sealed class CombatScreen : Screen
{
    public const int ViewId = 24000;
    public const int PaletteIndex = 5;

    private const int Field = 24001;
    private const int BarPicture = 24002;
    private const int Minimap = 24003;
    private const int Prompt = 24004;
    private const int FirstCommand = 24100;
    private const int FormationControls = 24103;
    private const int Escort = 24105;
    private const int FighterControls = 24106;
    private const int Retreat = 24107;
    private const int FirstFighterCommand = 24108;
    private const int FirstFormation = 24112;
    private const int LastCommand = 24115;
    private const int Auto = 24150;
    private const int Controls = 24151;
    private const int FirstFlag = 24501;
    private const int Flags = 8;
    private const byte Off = 17;
    private const byte On = 18;

    private readonly GameSession _session;
    private readonly BattleFieldPane _field;
    private bool _fighterPage;
    private bool _formationPage;
    private bool _auto;
    private string _savedPrompt = "";

    public CombatScreen(UiApp app)
        : base(app, new ViewWindow(app.Assets, ViewId, PaletteIndex, ViewId, MakePanel))
    {
        app.Assets.Cursor.Show(MouseCursor.Arrow);
        _session = app.Session ?? throw new InvalidOperationException("The battle window needs a game.");
        _field = Window.Get<BattleFieldPane>(Field);
        _field.Session = _session;
        _field.KeyStep = app.Preferences.Get(Preferences.CombatKeyScroll);
        _field.Send = order => _session.Send(new BattleOrderCommand(_session.Local.Index, order));
        var minimap = Window.Get<BattleMinimapPane>(Minimap);
        minimap.Session = _session;
        minimap.Field = _field;
        Open();
        SetBar(_session.CombatControls);

        app.Sound.Play(Audio.SoundIds.BattleWindowOpens);
        _ = Window.TakeMessages().ToList();
    }

    private static Pane? MakePanel(ViewNode node, int left, int top)
    {
        return node.ClassId switch
        {
            24001 => new BattleFieldPane(node, left, top),
            24002 => new BattleMinimapPane(node, left, top),
            _ => null,
        };
    }

    /// <summary>Shows the first battle under way, centered on the local side.</summary>
    private void Open()
    {
        var battle = _session.Game.Battles.FirstOrDefault();
        _field.Attach(battle);
        _fighterPage = false;
        _formationPage = false;
        Window.Get(Auto).Enabled = battle is not null;
        _auto = battle?.AutoEngaged ?? false;
        Window.Get<ButtonPane>(Auto).Color = _auto ? On : Off;
        _field.CenterOnLocalSide();
        ShowFlags(battle);
        Pages(0);
    }

    /// <summary>The flags of the players taking part, in turn.</summary>
    private void ShowFlags(Sim.Combat.Battle? battle)
    {
        var players = battle is null ? [] : Enumerable.Range(0, Sim.Combat.Battle.Players)
            .Where(p => battle.Commanders[p] is not null && p < _session.Game.Players.Count).ToList();
        for (var i = 0; i < Flags; i++)
        {
            var flag = Window.Get<PicturePane>(FirstFlag + i);
            flag.Set = ImageSet.Empire;
            flag.Picture = i < players.Count ? _session.Game.Players[players[i]].Flag + 1000 : 0;
        }
    }

    /// <summary>
    /// Widens at the controls panel's first button slot, where the prompt
    /// line below is plain black (docs/ui/widescreen.md): the battlefield shows more of the field
    /// across; the panel's slot row repeats a slot (52 wide) and the bottom
    /// box its cells, the width growing by whole slots; the buttons, the
    /// indicators and AUTO move over. A span on the panel's left edge was
    /// tried first; it smeared the edge's grille.
    /// </summary>
    public override WideLayout? WideAt { get; } = new([(191, 2)], [(363, 428, 52), (456, 476, 15)], null, 52);

    /// <summary>The indicators and the slot buttons keep their size and spacing, all moved over together.</summary>
    protected override void Widened(int extra, int extraHeight)
    {
        foreach (var id in Enumerable.Range(24501, 8).Concat(Enumerable.Range(24100, 16)))
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
        switch (message.Message)
        {
            case FirstCommand:
                _field.SetMode(1);
                break;
            case FirstCommand + 1:
                _field.SetMode(4);
                break;
            case FirstCommand + 2:
                _field.SetMode(3);
                break;
            case FormationControls:
                FormationPage(!_formationPage);
                break;
            case FirstCommand + 4:
                _field.SetMode(2);
                break;
            case Escort:
                // The Escort button has no case in the original: it does nothing.
                break;
            case FighterControls:
                FighterPage(!_fighterPage);
                break;
            case Retreat:
                _field.SetMode(13);
                break;
            case >= FirstFighterCommand and < FirstFormation:
                FighterPage(false);
                _field.SetMode(5 + message.Message - FirstFighterCommand);
                break;
            case >= FirstFormation and <= LastCommand:
                FormationPage(false);
                _field.SetMode(9 + message.Message - FirstFormation);
                break;
            case Auto when _field.Battle is not null:
                _auto = !_auto;
                Window.Get<ButtonPane>(Auto).Color = _auto ? On : Off;
                _savedPrompt = "";
                _field.SetMode(14);
                break;
            case Controls:
                SetBar(!_session.CombatControls);
                Pages(_field.Selection.Count);
                Window.Get<WrappedTextPane>(Prompt).Text = _savedPrompt;
                break;
            case UiMessage.Within:
                Help(message.Value);
                break;
            case UiMessage.Left:
                Window.Get<WrappedTextPane>(Prompt).Text = _savedPrompt;
                break;
            case BattleFieldPane.SelectionMessage:
                _savedPrompt = "";
                Pages(message.Value);
                break;
            case BattleFieldPane.ModeMessage:
                ShowPrompt(message.Value);
                break;
            default:
                break;
        }
    }

    /// <summary>
    /// Follows the battles (0x3F8, 0x3F9 and 0x400): the window goes to the
    /// Sector Display when its battle ends, picks up a battle that starts
    /// while it shows none, and keeps AUTO's color with the side's.
    /// </summary>
    public override void Tick(long now)
    {
        var battles = _session.Game.Battles;
        if (_field.Battle is { } battle && !battles.Contains(battle))
        {
            App.Switch(ScreenMode.SectorDisplay);
            return;
        }

        if (_field.Battle is null && battles.Count > 0)
        {
            Open();
        }

        if (_field.Battle is { } shown && shown.AutoEngaged != _auto)
        {
            _auto = shown.AutoEngaged;
            Window.Get<ButtonPane>(Auto).Color = _auto ? On : Off;
        }

        // The field takes the keys whatever was last clicked.
        Window.SetFocus(_field, now);
    }

    /// <summary>The control bar shown or hidden; the choice is kept for the next battle.</summary>
    private void SetBar(bool shown)
    {
        _session.CombatControls = shown;
        foreach (var id in (int[])[BarPicture, Minimap, Prompt])
        {
            var pane = Window.Get(id);
            pane.Visible = shown;
            pane.Enabled = shown;
        }

        for (var id = FirstCommand; id <= LastCommand; id++)
        {
            var pane = Window.Get(id);
            pane.Visible = shown && id < FirstFighterCommand;
            pane.Enabled = false;
        }

        _field.SetHeight(shown ? BattleFieldPane.BarHeight : BattleFieldPane.FullHeight);
        Window.Get<ButtonPane>(Controls).Color = shown ? On : Off;
    }

    /// <summary>
    /// The command buttons for what is chosen: one ship gets the
    /// main page but formations, several get formations too; Fighter
    /// Controls shows when the chosen ships have fighter bays.
    /// </summary>
    private void Pages(int count)
    {
        if (!_fighterPage && !_formationPage && !_session.CombatControls)
        {
            return;
        }

        _fighterPage = false;
        _formationPage = false;
        for (var id = FirstCommand; id <= LastCommand; id++)
        {
            Show(id, false);
        }

        if (count > 0)
        {
            for (var id = FirstCommand; id <= Retreat; id++)
            {
                Show(id, id != FighterControls && (count > 1 || id != FormationControls));
            }
        }

        var bays = _field.Selection.Sum(s => s.Ship.Fighters.Sum(b => b.Operational)) & 0xFF;
        Show(FighterControls, bays != 0);
        Window.Get<WrappedTextPane>(Prompt).Text = "";
    }

    private void Show(int id, bool shown)
    {
        var pane = Window.Get(id);
        pane.Visible = shown;
        pane.Enabled = shown;
    }

    /// <summary>
    /// A page of four buttons over the first four.
    /// Quirk kept: closing shows all sixteen and hides only the page's own.
    /// </summary>
    private void Page(int first, bool open)
    {
        for (var id = FirstCommand; id <= LastCommand; id++)
        {
            Show(id, !open);
        }

        for (var id = first; id < first + 4; id++)
        {
            Show(id, open);
        }
    }

    private void FighterPage(bool open)
    {
        _fighterPage = open;
        Page(FirstFighterCommand, open);
    }

    private void FormationPage(bool open)
    {
        _formationPage = open;
        Page(FirstFormation, open);
    }

    /// <summary>The prompt for a waiting command; the buttons hide while it waits.</summary>
    private void ShowPrompt(int mode)
    {
        if (mode != 0)
        {
            Pages(0);
        }

        int? index = mode switch
        {
            1 => 0,
            2 => 1,
            3 => 2,
            4 => 3,
            >= 9 and <= 12 => 4,
            5 => 5,
            6 => 6,
            _ => null,
        };
        _savedPrompt = index is { } i ? App.Assets.Strings.Get(new TextRef(25001, i)) : "";
        Window.Get<WrappedTextPane>(Prompt).Text = _savedPrompt;
    }

    /// <summary>A button's name in the prompt line while the pointer is on it.</summary>
    private void Help(int id)
    {
        int? index = id switch
        {
            >= FirstCommand and <= LastCommand => id - FirstCommand,
            Auto => 16,
            Controls => 17,
            _ => null,
        };
        // A button hidden by a command just chosen no longer counts (the original hides it at once).
        if (index is { } i && Window.Get(id).Shown)
        {
            Window.Get<WrappedTextPane>(Prompt).Text = App.Assets.Strings.Get(new TextRef(25002, i));
        }
    }
}
