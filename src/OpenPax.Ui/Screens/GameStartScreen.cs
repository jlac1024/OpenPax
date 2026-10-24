namespace OpenPax.Ui.Screens;

/// <summary>
/// The Game Start window (VIEW 31000, palette 1): the five main buttons,
/// the single player choices that replace them, the port's multiplayer
/// choices that replace them the same way, and the saved game group.
/// The original picks this or a second variant (VIEW 31001, palette 15) by
/// the clock; the port always shows the first.
/// </summary>
public sealed class GameStartScreen : Screen
{
    public const int ViewId = 31000;
    public const int PaletteIndex = 1;

    private const int SinglePlayer = 31100;
    private const int Multiplayer = 31101;
    private const int ReplayIntro = 31102;
    private const int Credits = 31103;
    private const int Exit = 31104;
    private const int NewGame = 31105;
    private const int LoadGame = 31106;
    private const int PreviousMenu = 31107;
    private const int LoadGroup = 31108;
    private const int LoadButton = 31109;
    private const int LoadGroupCancel = 31110;
    private const int SaveList = 31111;
    private const int SaveActivated = 31211;

    /// <summary>The port's own Options button, an id the original's view does not use.</summary>
    private const int OptionsButton = 31190;

    /// <summary>The port's Licenses button (the open source and attributions window), another id of its own.</summary>
    private const int LicensesButton = 31191;

    private static readonly int[] _mainButtons = [SinglePlayer, Multiplayer, ReplayIntro, Credits, Exit];
    private static readonly int[] _singlePlayerButtons = [NewGame, LoadGame, PreviousMenu];

    // The original's network choices were its own screens (modes 2 to 7).
    // The port's lobby choices take the main buttons' places instead, with
    // the port's captions, so they look like the single player choices.
    // The buttons the multiplayer menu takes over, top down.
    private static readonly int[] _multiplayerSlots = [SinglePlayer, Multiplayer, ReplayIntro, Credits];

    private readonly Widgets.ButtonPane _options;
    private readonly Widgets.ButtonPane _licenses;
    private readonly (int Left, int Top, int Width, int Height)[] _mainPlaces;
    private bool _multiplayerMenu;

    public GameStartScreen(UiApp app)
        : base(app, new ViewWindow(app.Assets, ViewId, PaletteIndex, ViewId))
    {
        app.Assets.Cursor.Show(MouseCursor.GameStart);

        // The port's Options joins the main buttons: a copy of Show Credits
        // with its own id and caption, the six spread over the five's span.
        _mainPlaces = [.. _mainButtons.Select(id => Window.Get(id)).Select(b => (b.Left, b.Top, b.Width, b.Height))];
        var credits = Window.Get<Widgets.ButtonPane>(Credits);
        _options = new Widgets.ButtonPane(credits.Node with { Base = credits.Node.Base with { Id = OptionsButton } }, credits.Left, credits.Top, credits.Details)
        {
            Caption = UiText.MenuOptions,
        };
        Window.AddOverlay(_options);

        // Licenses sits apart at the bottom, under the menu's column, where it does not crowd the game's own buttons.
        _licenses = new Widgets.ButtonPane(credits.Node with { Base = credits.Node.Base with { Id = LicensesButton } }, credits.Left, credits.Top, credits.Details)
        {
            Caption = UiText.MenuLicenses,
        };
        Window.AddOverlay(_licenses);
        foreach (var b in _mainButtons.Select(id => Window.Get<Widgets.ButtonPane>(id)).Append(_options).Append(_licenses))
        {
            b.PictureIsBackground = true;
        }

        var column = _mainPlaces[0];
        _licenses.Place(column.Left, 480 - column.Height - 6, column.Width, column.Height);

        PlaceMainButtons(withOptions: true);
        if (app.OpenMultiplayerMenu)
        {
            app.OpenMultiplayerMenu = false;
            ShowMultiplayerMenu(true);
        }
    }

    /// <summary>The port's Options button, which the view's own lookup does not hold.</summary>
    public Widgets.ButtonPane OptionsPane => _options;

    /// <summary>The port's Licenses button.</summary>
    public Widgets.ButtonPane LicensesPane => _licenses;

    public override void Handle(UiMessage message, long now)
    {
        if (_multiplayerMenu)
        {
            HandleMultiplayerMenu(message.Message);
            return;
        }

        switch (message.Message)
        {
            case SinglePlayer:
                Show(_mainButtons, false);
                _options.Visible = false;
                _licenses.Visible = false;
                Show(_singlePlayerButtons, true);
                break;
            case PreviousMenu:
                Show(_singlePlayerButtons, false);
                Show(_mainButtons, true);
                _options.Visible = true;
                _licenses.Visible = true;
                break;
            case LicensesButton:
                App.Switch(ScreenMode.Licenses);
                break;
            case OptionsButton:
                App.Switch(ScreenMode.Options);
                break;
            case LoadGame:
                Show(_singlePlayerButtons, false);
                Window.Get(LoadGroup).Visible = true;
                FillSaves();
                break;
            case SaveList:
                // A save that needs mods not turned on cannot be loaded; its mark says which.
                Window.Get(LoadButton).Enabled = Window.Get<Widgets.ListBoxPane>(SaveList).Selected is Widgets.SaveRow { Blocked: false };
                break;
            case LoadButton or SaveActivated:
                // the picked save, if any.
                if (Window.Get<Widgets.ListBoxPane>(SaveList).Selected is Widgets.SaveRow row)
                {
                    App.LoadGame(row.Name, now);
                }

                break;
            case LoadGroupCancel:
                Window.Get(LoadGroup).Visible = false;
                Show(_singlePlayerButtons, true);
                break;
            case ReplayIntro:
                App.Request(new HostRequest(HostRequestKind.PlayMovie, "PAX2.mp4"));
                break;
            case Exit:
                App.Request(new HostRequest(HostRequestKind.Quit));
                break;
            case NewGame:
                App.Switch(ScreenMode.SpeciesSelection);
                break;
            case Multiplayer when App.Lobby is not null:
                App.Switch(ScreenMode.LobbyRooms);
                break;
            case Multiplayer:
                ShowMultiplayerMenu(true);
                break;
            case Credits:
                // The credits screen is not ported yet.
                break;
        }
    }

    /// <summary>
    /// The multiplayer menu's entries, top down: LAN, the public lobby when
    /// config.json names one, a private lobby, then back (None). With no
    /// public lobby the rest move up, so no empty place is left.
    /// </summary>
    private List<(string Caption, Forms.LobbyChoice Choice)> MultiplayerEntries()
    {
        var entries = new List<(string, Forms.LobbyChoice)> { (UiText.MenuLan, Forms.LobbyChoice.Lan) };
        if (App.PublicLobbyAddress is not null)
        {
            entries.Add((UiText.MenuPublicLobby, Forms.LobbyChoice.Public));
        }

        entries.Add((UiText.MenuPrivateLobby, Forms.LobbyChoice.Private));
        entries.Add((UiText.MenuPreviousMenu, Forms.LobbyChoice.None));
        return entries;
    }

    /// <summary>The port's own: a lobby choice opens its connect page; Previous Menu brings the main buttons back.</summary>
    private void HandleMultiplayerMenu(int button)
    {
        var slot = Array.IndexOf(_multiplayerSlots, button);
        var entries = MultiplayerEntries();
        if (slot < 0 || slot >= entries.Count)
        {
            return;
        }

        var choice = entries[slot].Choice;
        if (choice == Forms.LobbyChoice.None)
        {
            ShowMultiplayerMenu(false);
        }
        else
        {
            App.LobbyChoice = choice;
            App.LobbyStatus = null;
            App.Switch(ScreenMode.LobbyConnect);
        }
    }

    private void ShowMultiplayerMenu(bool shown)
    {
        _multiplayerMenu = shown;
        var entries = MultiplayerEntries();
        for (var i = 0; i < _multiplayerSlots.Length; i++)
        {
            var button = Window.Get<Widgets.ButtonPane>(_multiplayerSlots[i]);
            button.Caption = shown && i < entries.Count ? entries[i].Caption : null;
            button.Visible = !shown || i < entries.Count;
        }

        Window.Get(Exit).Visible = !shown;
        _options.Visible = !shown;
        _licenses.Visible = !shown;
        PlaceMainButtons(withOptions: !shown);
    }

    /// <summary>The main buttons where the original puts them, or six evenly over the same span with Options third.</summary>
    private void PlaceMainButtons(bool withOptions)
    {
        if (!withOptions)
        {
            for (var i = 0; i < _mainButtons.Length; i++)
            {
                var (left, top, width, height) = _mainPlaces[i];
                Window.Get(_mainButtons[i]).Place(left, top, width, height);
            }

            return;
        }

        Widgets.Pane[] order = [Window.Get(SinglePlayer), Window.Get(Multiplayer), _options, Window.Get(ReplayIntro), Window.Get(Credits), Window.Get(Exit)];
        var (first, last) = (_mainPlaces[0], _mainPlaces[^1]);
        for (var i = 0; i < order.Length; i++)
        {
            var top = first.Top + ((last.Top - first.Top) * i / (order.Length - 1));
            order[i].Place(first.Left, top, first.Width, first.Height);
        }
    }

    /// <summary>The saved games, read backwards as the Game Options lists are; LOAD waits for a pick.</summary>
    private void FillSaves()
    {
        var list = Window.Get<Widgets.ListBoxPane>(SaveList);
        list.Clear();
        var names = App.SaveGames?.Names() ?? [];
        for (var i = names.Count - 1; i >= 0; i--)
        {
            list.Add(new Widgets.SaveRow(names[i]) { Warning = App.SaveWarning(names[i]), Blocked = App.SaveBlocked(names[i]) });
        }

        Window.Get(LoadButton).Enabled = false;
    }

    private void Show(int[] ids, bool visible)
    {
        foreach (var id in ids)
        {
            Window.Get(id).Visible = visible;
        }
    }
}
