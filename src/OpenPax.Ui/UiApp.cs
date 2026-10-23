using OpenPax.Audio;
using OpenPax.Data.Tables;
using OpenPax.Data.Views;
using OpenPax.Forms;
using OpenPax.Forms.Input;
using OpenPax.Net;
using OpenPax.Net.Lobby;
using OpenPax.Net.Server;
using OpenPax.Render;
using OpenPax.Render.TrueColor;
using OpenPax.Sim;
using OpenPax.Sim.Setup;
using OpenPax.Sim.Time;
using OpenPax.Ui.Forms;
using OpenPax.Ui.Network;
using OpenPax.Ui.Screens;
using OpenPax.Ui.Screens.Lobby;
using OpenPax.Ui.Widgets;

namespace OpenPax.Ui;

/// <summary>
/// The app as the screens see it (the original's app object):
/// the current screen, the switch between screens with its way back, the
/// message log, and what carries over from one screen to the next, such as
/// the chosen species. The host feeds it input and time and carries out its
/// requests.
/// </summary>
public sealed class UiApp
{
    private readonly Queue<HostRequest> _requests = [];
    private readonly List<ScreenMode> _backStack = [];
    private IReadOnlyList<RaceTemplate>? _stockSpecies;
    private TopBarPane? _topBar;

    /// <summary>
    /// How wide the original screens are drawn now, in their own pixels: 640,
    /// or more for a screen that grows in a larger window (docs/ui/widescreen.md).
    /// </summary>
    public int ClassicWidth { get; private set; } = 640;

    /// <summary>How tall: 480, or more for a screen that grows in height.</summary>
    public int ClassicHeight { get; private set; } = 480;

    /// <summary>Window pixels per original pixel: a whole number, so art and text land on the window's own pixels.</summary>
    public int FrameScale { get; private set; } = 1;

    /// <summary>
    /// Fits the original screens to the window (docs/ui/widescreen.md). The
    /// scale is the largest whole number at which 640x480 fits, kept even
    /// when the upscaled art is on (which is drawn at 2x and its multiples).
    /// From 2x up, a screen that grows is as large as the window at that
    /// scale, across up to 21:9 and down only when it has row spans; any
    /// other stays 640x480 between bars. Below 2x every screen stays
    /// 640x480, scaled to fill the window. Called each frame; the work is done only
    /// when something changes.
    /// </summary>
    public void FitWindow(int outputWidth, int outputHeight)
    {
        var fit = Math.Min(outputWidth / 640, outputHeight / 480);
        var scale = fit < 1 ? 1 : Preferences.Flag(Preferences.UpscaledArt) && fit >= 2 ? fit / 2 * 2 : fit;

        // A screen grows only in a window that shows it at 2x or more. Below
        // that the original 640x480 is scaled to fill the window instead,
        // its shape kept (ClassicPlacement): wider would only shrink it.
        var wide = fit >= 2 ? Current.WideAt : null;
        var height = wide is null || wide.Rows.Count == 0 ? 480 : Math.Max(480, outputHeight / scale);
        var room = wide is null ? 640 : Math.Clamp(outputWidth / scale, 640, Math.Max(640, height * 21 / 9));
        var step = Math.Max(1, wide?.WidthStep ?? 2);
        var width = 640 + ((room - 640) / step * step);
        if (scale != FrameScale || width != ClassicWidth || height != ClassicHeight || Current.Window.Extra != width - 640 || Current.Window.ExtraHeight != height - 480)
        {
            (FrameScale, ClassicWidth, ClassicHeight) = (scale, width, height);
            Current.Widen(width - 640, height - 480);
        }
    }

    private long _now;

    public UiApp(UiAssets assets)
    {
        Assets = assets;
        Forms = new FormHost(GameTheme.Build(assets));
        Preferences.Changed += PreferenceChanged;
        Current = new GameStartScreen(this);
    }

    /// <summary>The player's settings (app +0x60).</summary>
    public Preferences Preferences { get; } = new();

    /// <summary>The mods turned on, in their order (docs/modding.md); written to the config, used when the game next starts.</summary>
    public List<string> EnabledMods { get; } = [];

    /// <summary>The mods in the mods folder, for Options to list: id, name, version and what it says of itself.</summary>
    public List<ModListing> AvailableMods { get; } = [];

    /// <summary>The mods this run of the game loaded, as the lobby compares them ("id@version" in order); empty for none.</summary>
    public string ModFingerprint { get; set; } = "";

    /// <summary>The host's last frame time in milliseconds, for the console's fps command.</summary>
    public double FrameMs { get; set; }

    /// <summary>The port's developer console (`).</summary>
    public DevConsole Console => _console ??= new DevConsole(this);

    private DevConsole? _console;

    /// <summary>Sound and music, given by the host; volumes and the screen's music follow at once.</summary>
    public ISoundPlayer Sound
    {
        get => Assets.Sound;
        set
        {
            Assets.Sound = value;
            ApplyVolumes();
            RequestMusic(Mode);
        }
    }

    // Battles with the local side, for their voice and music.
    private readonly HashSet<Sim.Combat.Battle> _localBattles = [];

    /// <summary>
    /// The volumes from the preferences. Port decision
    /// (docs/decisions.md): the music volume applies from the start; the
    /// original applied it only once its slider was moved.
    /// </summary>
    public void ApplyVolumes()
    {
        Sound.SetVolumes(Preferences.Get(Preferences.SoundFxVolume), Preferences.Get(Preferences.VoiceVolume), Preferences.Get(Preferences.RedbookVolume));
    }

    /// <summary>Each screen asks for its music as it opens: the title, the setup screens', or the game's; the battle window asks for none.</summary>
    private void RequestMusic(ScreenMode mode)
    {
        int? track = mode switch
        {
            ScreenMode.GameStart => 2,
            ScreenMode.SpeciesSelection or ScreenMode.PlayerDesign or ScreenMode.GameSetup or ScreenMode.LobbyConnect or ScreenMode.LobbyRooms or ScreenMode.LobbyRoom => 3,
            ScreenMode.Options => 2,
            ScreenMode.Combat => null,
            _ => 14,
        };
        if (track is { } t)
        {
            Sound.RequestMusic(t);
        }
    }

    /// <summary>The saved games, given by the host; null when there is nowhere to keep them.</summary>
    public ISaveGames? SaveGames { get; set; }

    /// <summary>The game data, given by the host; a network client needs it to read the games it is sent.</summary>
    public Func<GameTables>? Tables { get; set; }

    /// <summary>The port's own forms, drawn in true color over the original screens (docs/ui/forms.md).</summary>
    public FormHost Forms { get; private set; }

    /// <summary>The host draws the pointer on the canvas, where it can go anywhere in the window; set by the host.</summary>
    public bool CursorOnCanvas { get; set; }

    /// <summary>The mouse in canvas pixels, while over the window.</summary>
    public (int X, int Y)? CanvasPointer { get; set; }

    private ChatBar? _chat;
    private readonly OpenPax.Forms.Drawing.WarpField _warp = new();

    /// <summary>The lobby this app is connected to, or null.</summary>
    public LobbyClient? Lobby { get; set; }

    /// <summary>The private lobby last typed, kept for the next visit.</summary>
    public string LobbyAddress { get; set; } = "";

    /// <summary>The public lobby's address (config.json beside the game).</summary>
    public string PublicLobbyAddress { get; set; } = "127.0.0.1:" + NetProtocol.DefaultLobbyPort.ToString(System.Globalization.CultureInfo.InvariantCulture);

    /// <summary>The lobby choice made on Game Start's multiplayer menu, whose page the connect screen shows (and reopens when a lobby is lost).</summary>
    public LobbyChoice LobbyChoice { get; set; } = LobbyChoice.Private;

    /// <summary>Game Start opens on its multiplayer choices next time (coming back from the lobby screens).</summary>
    public bool OpenMultiplayerMenu { get; set; }

    /// <summary>Hosts the LAN scan asks directly besides its broadcasts and VPN peers (config.json's lanHosts).</summary>
    public List<string> LanHosts { get; } = [];

    /// <summary>Places the LAN scan asks besides the others (a test's own machine on its own port).</summary>
    public List<System.Net.IPEndPoint> LanScanAlso { get; } = [];

    /// <summary>The player's name (Options): what others see in a lobby, and the name Game Setup starts on.</summary>
    public string PlayerName { get; set; } = UiText.DefaultPlayerName;

    /// <summary>Why the lobby was left, for the connect screen to show next; null when nothing went wrong.</summary>
    public string? LobbyStatus { get; set; }

    /// <summary>The lobby's chat as heard, outside rooms and in them; kept across the lobby screens.</summary>
    public List<string> LobbyChat { get; } = [];

    /// <summary>The name the game autosaves under (and on quitting).</summary>
    public const string AutoSaveName = "PaxAutoSaveGame";

    /// <summary>The name of the quick save keys' game (Ctrl F1, Ctrl F2).</summary>
    public const string QuickSaveName = "QuickSave";

    public UiAssets Assets { get; private set; }

    public Screens.Screen Current { get; private set; }

    /// <summary>The popup over the screen, if one is open (app +0x70).</summary>
    public IPopup? Popup { get; private set; }

    /// <summary>The window that takes input: the popup's when one is open.</summary>
    private ViewWindow Input => Popup?.Window ?? Current.Window;

    public ScreenMode Mode { get; private set; } = ScreenMode.GameStart;

    /// <summary>The species chosen or designed, kept for the setup screen (app +0x80).</summary>
    public RaceTemplate? ChosenSpecies { get; set; }

    /// <summary>The species the designer starts from (app +0x84).</summary>
    public RaceTemplate? DesignTemplate { get; set; }

    /// <summary>The game being played, once started.</summary>
    public GameSession? Session { get; private set; }

    /// <summary>The message log (app +0x74).</summary>
    public MessageLog Log { get; } = new();

    /// <summary>The current notice's text, or nothing.</summary>
    public string Notice => Log.Current?.Text ?? "";

    /// <summary>The screens to go back to, most recent last (the log's back stack, +0x50).</summary>
    public IReadOnlyList<ScreenMode> BackStack => _backStack;

    /// <summary>Where designed species are kept, given by the host; null keeps them for this run only.</summary>
    public ICustomSpecies? CustomSpeciesStore { get; set; }

    private List<RaceTemplate>? _customSpecies;

    /// <summary>
    /// Designed species, oldest first. Quirk kept from the
    /// original's list of 100: more are not loaded (the original overruns).
    /// </summary>
    public List<RaceTemplate> CustomSpecies => _customSpecies ??= [.. (CustomSpeciesStore?.Load() ?? []).Take(MaxCustomSpecies)];

    public const int MaxCustomSpecies = 100;

    /// <summary>Writes the designed species back.</summary>
    public void SaveCustomSpecies() => CustomSpeciesStore?.Save(CustomSpecies);

    /// <summary>The eight built-in species, RACE records 1 to 8 of Data/GameData in order.</summary>
    public IReadOnlyList<RaceTemplate> StockSpecies => _stockSpecies ??= Assets.LoadStockSpecies();

    /// <summary>
    /// Switches screens: nothing happens if already there, or
    /// for a screen not ported yet; otherwise the new one is built. With
    /// push, the game screen left is remembered for the back button
    /// ; leaving the bridge forgets them all. Game windows from
    /// the Sector Display on get the top bar, last, so it is on top.
    /// </summary>
    public void Switch(ScreenMode mode, bool push = false)
    {
        if (mode == Mode || !Ported(mode))
        {
            return;
        }

        if (push && Mode > ScreenMode.GameSetup)
        {
            if (Mode == ScreenMode.MainControl)
            {
                _backStack.Clear();
            }
            else if (_backStack.Count == 0 || _backStack[^1] != Mode)
            {
                _backStack.Add(Mode);
            }
        }

        var fromOptions = Mode == ScreenMode.Options;
        Open(mode);
        if (fromOptions)
        {
            ApplyOwnMods();
        }
    }

    /// <summary>
    /// Leaving Options with the mods changed, and no game running, loads the
    /// new set at once; with a game running it waits for the next start.
    /// </summary>
    private void ApplyOwnMods()
    {
        var wanted = string.Join(',', EnabledMods);
        if (Session is null && LoadedModSet is null && LoadMods is not null && wanted != _ownModsLoaded)
        {
            LoadMods(null);
            _ownModsLoaded = wanted;
        }
    }

    // The player's own mod ids as last loaded, to see whether Options changed them.
    private string? _ownModsLoaded;

    /// <summary>The host loaded the player's own mods (EnabledMods as they are now).</summary>
    public void OwnModsLoaded() => _ownModsLoaded = string.Join(',', EnabledMods);

    /// <summary>The screen of a mode made afresh, the one up closed first.</summary>
    private void Open(ScreenMode mode)
    {
        Current.Closed();
        Mode = mode;
        Popup = null;
        Current = mode switch
        {
            ScreenMode.GameStart => new GameStartScreen(this),
            ScreenMode.Victory => new VictoryScreen(this),
            ScreenMode.SpeciesSelection => new SpeciesSelectionScreen(this),
            ScreenMode.PlayerDesign => new PlayerDesignScreen(this),
            ScreenMode.GameSetup => new GameSetupScreen(this),
            ScreenMode.MainControl => new MainControlScreen(this),
            ScreenMode.Research => new ResearchScreen(this),
            ScreenMode.PlanetarySurface => new PlanetarySurfaceScreen(this),
            ScreenMode.ShipDesign => new ShipDesignScreen(this),
            ScreenMode.DomesticPolicy => new DomesticPolicyScreen(this),
            ScreenMode.FleetCommand => new FleetCommandScreen(this),
            ScreenMode.ForeignAffairs => new ForeignAffairsScreen(this),
            ScreenMode.GameOptions => new GameOptionsScreen(this),
            ScreenMode.Combat => new CombatScreen(this),
            ScreenMode.Developer => new DeveloperScreen(this),
            ScreenMode.LobbyConnect => new LobbyConnectScreen(this),
            ScreenMode.LobbyRooms => new LobbyRoomsScreen(this),
            ScreenMode.LobbyRoom => new LobbyRoomScreen(this),
            ScreenMode.Options => new OptionsScreen(this),
            _ => new SectorDisplayScreen(this),
        };

        RequestMusic(mode);
        _topBar = null;
        if (mode >= ScreenMode.SectorDisplay)
        {
            _topBar = new TopBarPane(this);
            Current.Window.AddOverlay(_topBar);
        }
    }

    /// <summary>
    /// The port's own: another mod set's assets in place of these
    /// (docs/modding/multiplayer.md), once the host has loaded it. The forms
    /// and the screen up are made again from them; no game may be running.
    /// </summary>
    public void UseAssets(UiAssets assets)
    {
        Assets = assets;
        Forms = new FormHost(GameTheme.Build(assets)) { Scale = Forms.Scale };
        Open(Mode);
    }

    /// <summary>
    /// The host's way to load a mod set in place (Program): null for the
    /// player's own (EnabledMods), or a room's set of mod keys
    /// ("id@version#hash", in order). It gives back the keys it could not
    /// find, which have to be fetched first. Null where there is no host to
    /// do it (tests).
    /// </summary>
    public Func<string?, IReadOnlyList<string>>? LoadMods { get; set; }

    /// <summary>The mod set loaded now: null for the player's own, else the room's keys it was loaded for.</summary>
    public string? LoadedModSet { get; set; }

    /// <summary>As host, a package of one of the loaded mods by its key, to send a room's members; null for none (the host, Program).</summary>
    public Func<string, byte[]?>? PackMod { get; set; }

    /// <summary>A package from a room's host, kept with the mods fetched from hosts; true when it unpacked whole (the host, Program).</summary>
    public Func<string, byte[], bool>? InstallMod { get; set; }

    /// <summary>Keeps the player's mods the room host's while in another's room.</summary>
    internal Network.ModSync ModSync { get; } = new();

    private static bool Ported(ScreenMode mode)
    {
        return mode is ScreenMode.Victory or ScreenMode.GameStart or ScreenMode.SpeciesSelection or ScreenMode.PlayerDesign or ScreenMode.GameSetup
            or ScreenMode.MainControl or ScreenMode.SectorDisplay or ScreenMode.Research or ScreenMode.PlanetarySurface or ScreenMode.ShipDesign or ScreenMode.DomesticPolicy or ScreenMode.FleetCommand or ScreenMode.ForeignAffairs or ScreenMode.GameOptions or ScreenMode.Combat or ScreenMode.Developer
            or ScreenMode.LobbyConnect or ScreenMode.LobbyRooms or ScreenMode.LobbyRoom or ScreenMode.Options;
    }

    /// <summary>
    /// The back button: the most recent screen that is not this
    /// one, else the bridge. Nothing is pushed on the way back.
    /// </summary>
    public void Back()
    {
        var target = ScreenMode.MainControl;
        while (_backStack.Count > 0)
        {
            target = _backStack[^1];
            while (_backStack.Count > 0 && _backStack[^1] == target)
            {
                _backStack.RemoveAt(_backStack.Count - 1);
            }

            if (target != Mode)
            {
                break;
            }
        }

        Switch(target);
    }

    /// <summary>The back button with the right button: straight to the bridge, forgetting the way back.</summary>
    public void ToBridge()
    {
        _backStack.Clear();
        Switch(ScreenMode.MainControl);
    }

    internal void Request(HostRequest request)
    {
        _requests.Enqueue(request);
    }

    /// <summary>
    /// The host's new game, made from <see cref="HostRequestKind.StartGame"/>:
    /// the old history goes, the clock's speed is announced, and the bridge
    /// opens.
    /// </summary>
    public void GameStarted(Game game, long now, SavedSession? saved = null)
    {
        // A new game plays with the fixes chosen in Options; a loaded one keeps its own.
        if (saved is null && game.Tick == 0)
        {
            game.Fixes = Preferences.ChosenFixes();
        }

        Start(saved is null ? new GameSession(game, now) : new GameSession(saved, now), now);
    }

    /// <summary>A network game begins, hosted here or joined: the local player plays it through the link.</summary>
    public void NetworkGameStarted(Game game, Sim.Players.Player local, IGameLink link, long now)
    {
        Start(new GameSession(game, now, local, (link as HostLink)?.Server.Clock) { Link = link }, now);
    }

    /// <summary>
    /// The host's network game is made: the server runs here on it, takes a
    /// relay connection from the lobby for each member, and the host plays.
    /// </summary>
    public void HostedGameReady(Game game, long now)
    {
        if (Lobby?.Starting is not { } starting)
        {
            return;
        }

        // The host's fixes rule the network game; guests take them with its state.
        game.Fixes = Preferences.ChosenFixes();
        var server = new GameServer(game, new GameClock(now), host: 0);
        foreach (var ticket in starting.Tickets)
        {
            server.Accept(Lobby.OpenRelay(ticket, host: true));
        }

        NetworkGameStarted(game, game.Players[0], new HostLink(server), now);
    }

    private void Start(GameSession session, long now)
    {
        Session = session;
        ChosenSpecies = null;
        DesignTemplate = null;
        _backStack.Clear();
        Log.ClearHistory();
        _now = now;

        // making the clock sets its speed from the
        // preference, which posts a notice.
        var speed = Math.Clamp(Preferences.Get(Preferences.GameSpeed), 0, 6);
        Session.Clock.ZeroPauses = Preferences.Flag(Preferences.ZeroPauses);
        Session.Clock.SetSpeed(speed);
        PostSpeed(speed);
        Switch(ScreenMode.MainControl);
    }

    /// <summary>
    /// Loads a saved game: the game is replaced and the bridge
    /// opens with "Loaded game NAME successfully.". False when it cannot be read.
    /// </summary>
    public bool LoadGame(string name, long now)
    {
        if (SaveGames?.Load(name) is not { } saved)
        {
            return false;
        }

        // Whatever screen is up, the bridge opens afresh.
        Popup = null;
        Mode = ScreenMode.GameStart;
        GameStarted(saved.Game, now, saved);
        Console.CheatsOn = saved.CheatsOn;
        Post(UiText.LoadedGame + name + UiText.LoadedGameEnd);
        return true;
    }

    /// <summary>Saves the game, unless a battle is being fought.</summary>
    public bool SaveGame(string name)
    {
        return Session is { } session && !session.Game.InCombat && name.Length > 0 && (SaveGames?.Save(name, session.Snapshot(Console.CheatsOn)) ?? false);
    }

    /// <summary>NEW GAME: the game is dropped, with no question, and the start screen opens.</summary>
    public void NewGame()
    {
        Session = null;
        _backStack.Clear();
        Log.ClearHistory();
        Switch(ScreenMode.GameStart);
    }

    /// <summary>
    /// QUIT GAME and Alt X (app command 10): the credits, an autosave in a
    /// game when the preference is on, then the game closes. No question.
    /// </summary>
    public void QuitGame()
    {
        Request(new HostRequest(HostRequestKind.PlayMovie, "Pax2Credits.mp4"));
        if (Session is not null && Preferences.Flag(Preferences.AutoSaveGame))
        {
            SaveGame(AutoSaveName);
        }

        Current.Closed();
        Request(new HostRequest(HostRequestKind.Quit));
    }

    public int PaletteIndex => Current.Window.PaletteIndex;

    internal static FormKey? ToFormKey(UiKey key) => key switch
    {
        UiKey.Left => FormKey.Left,
        UiKey.Right => FormKey.Right,
        UiKey.Up => FormKey.Up,
        UiKey.Down => FormKey.Down,
        UiKey.Home => FormKey.Home,
        UiKey.End => FormKey.End,
        UiKey.Backspace => FormKey.Backspace,
        UiKey.Delete => FormKey.Delete,
        UiKey.Return => FormKey.Return,
        UiKey.Tab => FormKey.Tab,
        UiKey.Escape => FormKey.Escape,
        _ => null,
    };

    /// <summary>The true-color pass: the forms over the window, then the pointer.</summary>
    public void DrawCanvas(Canvas canvas, ClassicPlacement classic)
    {
        canvas.Clear(Color.Transparent);
        Forms.Scale = classic.Scale;
        if (Current is not FormScreen)
        {
            DrawSideBars(canvas, classic);
        }

        Forms.Draw(canvas);
        if (CursorOnCanvas && CanvasPointer is { } p)
        {
            Assets.Cursor.Draw(canvas, Assets, Assets.Palette(PaletteIndex), p.X, p.Y, classic.Scale, _now);
        }
    }

    /// <summary>
    /// The room a wide (or tall) window leaves beside the original screens'
    /// 4:3 picture: the starfield the port's own screens stand on, flying by
    /// when that is on (the menus' and the game's alike), edged with the
    /// panels' frame line.
    /// </summary>
    private void DrawSideBars(Canvas canvas, ClassicPlacement classic)
    {
        Span<(int X, int Y, int W, int H)> bars =
        [
            (0, 0, classic.X, canvas.Height),
            (classic.X + classic.Width, 0, canvas.Width - classic.X - classic.Width, canvas.Height),
            (classic.X, 0, classic.Width, classic.Y),
            (classic.X, classic.Y + classic.Height, classic.Width, canvas.Height - classic.Y - classic.Height),
        ];
        var theme = Forms.Theme;
        var moving = theme.Warp;
        foreach (var (x, y, w, h) in bars)
        {
            if (w <= 0 || h <= 0)
            {
                continue;
            }

            canvas.PushClip(x, y, w, h);
            canvas.FillRect(x, y, w, h, Color.Black);
            if (moving is not null)
            {
                // One field over the whole window, seen through each bar.
                moving.Draw(canvas, 0, 0, canvas.Width, canvas.Height, _now, classic.Scale);
            }
            else if (theme.Backdrop is { } tile)
            {
                for (var ty = 0; ty < canvas.Height; ty += tile.Height)
                {
                    for (var tx = 0; tx < canvas.Width; tx += tile.Width)
                    {
                        canvas.DrawImage(tile, tx, ty);
                    }
                }
            }

            canvas.PopClip();
        }

        if (theme.PanelFrame is { } frame && (classic.X > 0 || classic.Y > 0))
        {
            var t = classic.Scale;
            canvas.DrawRect(classic.X - t, classic.Y - t, classic.Width + (2 * t), classic.Height + (2 * t), frame, t);
        }
    }

    /// <summary>A form has the keyboard, so keys go to it and not to the game's key commands.</summary>
    public bool FormsHaveFocus => Forms.Top?.Focused is not null;

    // The mouse in canvas pixels, for the forms; each says whether a form took it.
    public bool FormsMouseMove(int x, int y)
    {
        CanvasPointer = (x, y);
        return Forms.MouseMove(x, y);
    }

    public bool FormsMouseDown(int x, int y, bool right) => Forms.MouseDown(x, y, right);

    public bool FormsMouseUp(int x, int y) => Forms.MouseUp(x, y);

    public bool FormsWheel(int delta) => Forms.Wheel(delta);

    public void MouseDown(int x, int y, int clicks, long now, bool right = false)
    {
        Pointer = (x, y);
        Input.MouseDown(x, y, clicks, now, right);
    }

    public void MouseMove(int x, int y, long now)
    {
        Pointer = (x, y);
        Input.MouseMove(x, y, now);
    }

    public void MouseUp(int x, int y)
    {
        Pointer = (x, y);
        Input.MouseUp(x, y);
    }

    /// <summary>Where the mouse is, once it has been seen over the window; null while it is away, so no pointer is drawn.</summary>
    public (int X, int Y)? Pointer { get; set; }

    public void Key(UiKey key, bool shift, long now)
    {
        if (Console.Open)
        {
            Console.Key(key);
            return;
        }

        // The chat line closes on Escape.
        if (_chat is { } chat && Forms.Top == chat.Form && key == UiKey.Escape)
        {
            chat.Close();
            _chat = null;
            return;
        }

        // A form on top takes the keys it uses.
        if (Forms.Top is not null && ToFormKey(key) is { } formKey && Forms.Key(formKey, shift))
        {
            return;
        }

        // Return opens the chat line in a network game's screens, unless a field is being typed in.
        if (key == UiKey.Return && Session?.Link is not null && Forms.Top is null && Popup is null
            && Mode >= ScreenMode.MainControl && !Current.Window.HasFocus)
        {
            _chat = ChatBar.Open(this);
            return;
        }

        if (Popup is { } popup)
        {
            if (popup.Key(key))
            {
                ClosePopup(popup);
            }

            return;
        }

        Current.Window.Key(key, shift, now);
    }

    public void Typed(string text, long now)
    {
        if (Console.Open)
        {
            foreach (var c in text)
            {
                Console.Typed(c);
            }

            return;
        }

        if (Forms.Top?.Focused is not null)
        {
            foreach (var c in text)
            {
                Forms.Text(c);
            }

            return;
        }

        Input.Typed(text, now);
    }

    /// <summary>
    /// Opens the yes or no popup over the screen ( with kind 0x1A).
    /// The history list closes and stays closed after.
    /// </summary>
    public void Confirm(string question, Action onYes, Action? onNo = null)
    {
        Open(new ConfirmPopup(Assets, PaletteIndex, question, onYes, ClassicWidth, ClassicHeight, onNo));
    }

    /// <summary>The port's own: a notice over the screen with an OK button (ConfirmPopup.Notice).</summary>
    public void Tell(string text)
    {
        Open(ConfirmPopup.Notice(Assets, PaletteIndex, text, ClassicWidth, ClassicHeight));
    }

    /// <summary>Opens a popup over the screen; the history list closes and stays closed.</summary>
    public void Open(IPopup popup)
    {
        _topBar?.CloseHistory();
        Popup = popup;
    }

    private void ClosePopup()
    {
        Popup = null;
    }

    // A popup's answer can open the next one (YES to an update opens its
    // download): only the one that answered closes.
    private void ClosePopup(IPopup answered)
    {
        if (Popup == answered)
        {
            Popup = null;
        }
    }

    /// <summary>
    /// A keyboard command. Screen keys work only in the game's
    /// screens (modes 11 to 21) and not over a popup; speed and pause also not
    /// in Game Options.
    /// </summary>
    public void Command(UiCommand command)
    {
        // The console takes the keys while open; Escape closes it.
        if (command == UiCommand.Console)
        {
            Console.Toggle();
            return;
        }

        if (Console.Open)
        {
            if (command == UiCommand.GameOptions)
            {
                Console.Close();
            }

            return;
        }

        // The game keys work on any screen, but not over a popup.
        if (Popup is null && command is UiCommand.Quit or UiCommand.QuickLoad or UiCommand.LoadAutoSave or UiCommand.NewGame)
        {
            switch (command)
            {
                case UiCommand.Quit:
                    QuitGame();
                    break;
                case UiCommand.QuickLoad or UiCommand.LoadAutoSave:
                    LoadGame(command == UiCommand.QuickLoad ? QuickSaveName : AutoSaveName, _now);
                    break;
                default:
                    if (Session is not null)
                    {
                        NewGame();
                    }

                    break;
            }

            return;
        }

        if (Session is not { } session || Mode < ScreenMode.MainControl)
        {
            return;
        }

        if (Popup is not null && command is not (UiCommand.SpeedUp or UiCommand.SlowDown or UiCommand.Pause))
        {
            return;
        }

        switch (command)
        {
            case UiCommand.Bridge:
                Switch(ScreenMode.MainControl, push: true);
                break;
            case UiCommand.QuickSave:
                // Said on the status line, not in a popup, which would hold up a quick load straight after.
                if (SaveGame(QuickSaveName))
                {
                    Post(string.Format(System.Globalization.CultureInfo.InvariantCulture, UiText.GameSavedNotice, QuickSaveName));
                }

                break;
            case UiCommand.SectorDisplay:
                Switch(ScreenMode.SectorDisplay, push: true);
                break;
            case UiCommand.Research:
                Switch(ScreenMode.Research, push: true);
                break;
            case UiCommand.FleetCommand:
                Switch(ScreenMode.FleetCommand, push: true);
                break;
            case UiCommand.DomesticPolicy:
                Switch(ScreenMode.DomesticPolicy, push: true);
                break;
            case UiCommand.ForeignAffairs:
                Switch(ScreenMode.ForeignAffairs, push: true);
                break;
            case UiCommand.ShipDesign:
                Switch(ScreenMode.ShipDesign, push: true);
                break;
            case UiCommand.PlanetarySurface:
                Switch(ScreenMode.PlanetarySurface, push: true);
                break;
            case UiCommand.Mode20:
                Switch(ScreenMode.Combat, push: true);
                break;
            case UiCommand.GameOptions:
                // Inside the window Escape does nothing: it is already the mode.
                Switch(ScreenMode.GameOptions, push: true);
                break;
            // In a network game the speed and pause keys do nothing, as in
            // the original; the host sets the speed in Options.
            case UiCommand.SpeedUp or UiCommand.SlowDown or UiCommand.Pause when session.Link is not null:
                break;
            case UiCommand.SpeedUp or UiCommand.SlowDown:
                var speed = Math.Clamp(session.Clock.Speed + (command == UiCommand.SpeedUp ? 1 : -1), 0, 6);
                Preferences.Set(Preferences.GameSpeed, speed, notify: false);
                session.Clock.SetSpeed(speed);
                PostSpeed(speed);
                break;
            case UiCommand.Pause:
                session.Clock.Paused = !session.Clock.Paused;
                Post(Assets.Strings.Get(new TextRef(75, session.Clock.Paused ? 17 : 18)));
                break;
            default:
                // The other screens are not ported yet.
                break;
        }
    }

    /// <summary>The app's preference listener: a new game speed reaches the clock with its notice.</summary>
    private void PreferenceChanged(string name)
    {
        if (name is Preferences.SoundFxVolume or Preferences.VoiceVolume or Preferences.RedbookVolume)
        {
            ApplyVolumes();
        }

        if (name == Preferences.ZeroPauses && Session is { } held)
        {
            held.Clock.ZeroPauses = Preferences.Flag(name);
        }

        if (name == Preferences.GameSpeed && Session is { } session)
        {
            var speed = Math.Clamp(Preferences.Get(name), 0, 6);

            // The host's speed 0 pauses everyone, so in a network game it asks them first (docs/netcode/modern.md).
            if (speed == 0 && session.Clock.ZeroPauses && session.Link is HostLink host && session.Clock.Speed != 0)
            {
                host.Server.RequestPause();
                if (host.Server.Voting)
                {
                    Post(UiText.PauseAsked);
                    return;
                }

                PostSpeed(session.Clock.Speed);
                return;
            }

            session.Clock.SetSpeed(speed);
            PostSpeed(speed);
        }
    }

    // the port's own: speed 0 says the game is paused when it pauses
    private void PostSpeed(int speed)
    {
        Post(speed == 0 && Session?.Clock.ZeroPauses == true
            ? Assets.Strings.Get(new TextRef(75, 17))
            : Assets.Strings.Get(new TextRef(71, 0)) + speed.ToString(System.Globalization.CultureInfo.InvariantCulture));
    }

    /// <summary>The network game's chat into the message line and its history, each line under its speaker's name.</summary>
    private void TakeChat()
    {
        if (Session is not { Link: { } link } session)
        {
            return;
        }

        foreach (var (from, text) in link.TakeChat())
        {
            var name = from >= 0 && from < session.Game.Players.Count ? session.Game.Players[from].Name : UiText.ChatServer;
            Log.Add(new UiNotice(name + ": " + text, 0, false, null), _now);
        }

        foreach (var news in link.TakeNews())
        {
            switch (news.Kind)
            {
                // As the original's speed order runs on each machine: the
                // preference takes the speed, and its watcher sets the clock
                // and posts the notice.
                case Net.Messages.SessionNews.SpeedChanged:
                    Preferences.Set(Preferences.GameSpeed, Math.Clamp(news.Value, 0, 6));
                    break;

                // The host asks to pause: each guest answers yes or no.
                case Net.Messages.SessionNews.PauseVoteOpened when news.Player >= 0 && news.Player < session.Game.Players.Count:
                    Confirm(string.Format(System.Globalization.CultureInfo.InvariantCulture, UiText.PauseVote, session.Game.Players[news.Player].Name), () => link.VotePause(true), () => link.VotePause(false));
                    break;

                case Net.Messages.SessionNews.PauseVoteClosed:
                    Post(news.Value == 1 ? Assets.Strings.Get(new TextRef(75, 17)) : UiText.PauseDeclined);

                    // A vote that failed leaves the host at the speed it had.
                    if (news.Value == 0 && link.IsHost)
                    {
                        Preferences.Set(Preferences.GameSpeed, session.Clock.Speed, notify: false);
                    }

                    break;

                // The original's player lost notice: the name, then the reason's line from list 86.
                case Net.Messages.SessionNews.PlayerLeft when news.Player >= 0 && news.Player < session.Game.Players.Count:
                    Post(session.Game.Players[news.Player].Name + Assets.Strings.Get(new TextRef(86, Math.Clamp(news.Value, 1, 3))));
                    break;
            }
        }
    }

    /// <summary>A plain notice: no date, transient, no action.</summary>
    public void Post(string text)
    {
        Log.Add(new UiNotice(text, 0, true, null), _now);
    }

    /// <summary>
    /// Time passes: the game ticks when due and its notices go to the log,
    /// the log moves on, the current screen handles its messages, and the
    /// host gets what was asked of it.
    /// </summary>
    public IEnumerable<HostRequest> Tick(long now)
    {
        _now = now;
        Forms.Theme.Warp = Preferences.Flag(Preferences.MovingStars) ? _warp : null;

        // StarSpeed 50 is the default speed, 100 twice it, 0 still.
        _warp.Speed = OpenPax.Forms.Drawing.WarpField.DefaultSpeed * Math.Clamp(Preferences.Get(Preferences.StarSpeed), 0, 100) / 50f;
        Forms.Tick(now);
        TakeChat();
        if (Session?.Tick(now) is { } events)
        {
            GameEvents(events);
        }

        FollowBattles();

        TakeNotices(now);
        Log.Tick(now);

        if (Popup is { } popup)
        {
            popup.Window.Tick(now);
            foreach (var message in popup.Window.TakeMessages().ToList())
            {
                if (popup.Handle(message))
                {
                    ClosePopup(popup);
                    break;
                }
            }
        }

        var screen = Current;
        screen.Window.Tick(now);
        foreach (var message in screen.Window.TakeMessages().ToList())
        {
            // A message can switch screens; the rest belong to the old one.
            if (Current != screen)
            {
                break;
            }

            screen.Handle(message, now);
        }

        // Orders given in this frame can tell the player something too.
        TakeNotices(now);
        Current.Tick(now);
        FollowUpdates();

        // In a room, the host's mods are loaded in place: after the screen's own tick, as that may make the screen again.
        ModSync.Tick(this);
        while (_requests.TryDequeue(out var request))
        {
            yield return request;
        }
    }

    /// <summary>Where checking for and installing updates stands; the host keeps it current (docs/updates.md).</summary>
    public UpdateState Updates { get; } = new();

    /// <summary>The player chose to update: the host downloads and installs it, the popup shows how far it has got.</summary>
    public void StartUpdate()
    {
        if (Updates.Available is null || Updates.Busy)
        {
            return;
        }

        // Busy until the host says otherwise, so the popup does not take the
        // frame before the host has the request for "stopped".
        Updates.Busy = true;
        Updates.Status = string.Format(System.Globalization.CultureInfo.InvariantCulture, UiText.UpdateStarting, Updates.Available);
        Request(new HostRequest(HostRequestKind.Update));
        Open(new UpdatePopup(Assets, PaletteIndex, () => Request(new HostRequest(HostRequestKind.CancelUpdate)), ClassicWidth, ClassicHeight));
    }

    // An update found is offered once, on the main menu with nothing open over
    // it; a download's popup follows it, and says why when it stops short.
    private void FollowUpdates()
    {
        if (Popup is UpdatePopup downloading)
        {
            downloading.Show(Updates);
            if (!Updates.Busy)
            {
                ClosePopup();
                Tell(Updates.Status);
            }

            return;
        }

        if (Updates is { Available: { } version, Offered: false, Busy: false } && Current is Screens.GameStartScreen && Popup is null && Forms.Top is null)
        {
            Updates.Offered = true;
            Confirm(string.Format(System.Globalization.CultureInfo.InvariantCulture, UiText.UpdateOffer, version, Updates.Running), StartUpdate);
        }
    }

    /// <summary>The game's notices for this player become log entries; first contact also plays its movie.</summary>
    private void TakeNotices(long now)
    {
        if (Session is not { } session)
        {
            return;
        }

        foreach (var notice in session.TakeNotices())
        {

            Log.Add(NoticeText.Make(this, session, notice), now);
            if (NoticeVoice(notice.Kind) is { } voice)
            {
                Sound.Play(voice);
            }

            if (notice.Kind == NoticeKind.FirstContact)
            {
                Request(new HostRequest(HostRequestKind.PlayMovie, "First Contact.mp4"));
            }
        }
    }

    /// <summary>The voice a notice for the local player speaks; most are silent.</summary>
    private static int? NoticeVoice(NoticeKind kind) => kind switch
    {
        NoticeKind.ResearchCompleted => SoundIds.ResearchComplete,
        NoticeKind.ShipFinished => SoundIds.ShipComplete,
        NoticeKind.ColonyLost => SoundIds.PlanetDefeated,
        NoticeKind.SpySucceeded => SoundIds.SpySucceeded,
        NoticeKind.SpyFailed => SoundIds.SpyFailed,
        NoticeKind.SpyStopped => SoundIds.SpyCaught,
        NoticeKind.TradeOffered => SoundIds.TradePending,
        NoticeKind.TradeAccepted => SoundIds.TradeAccepted,
        NoticeKind.TradeRejected => SoundIds.TradeDeclined,
        _ => null,
    };

    /// <summary>
    /// A battle with the local side starts: "battle stations" and the battle
    /// music ((1)); one ends: the game's music again
    /// . The battle window opens only when the player asks.
    /// </summary>
    private void FollowBattles()
    {
        var battles = Session?.Game.Battles ?? [];
        var local = Session?.Local.Index ?? -1;
        foreach (var battle in battles)
        {
            if (local >= 0 && battle.Commanders[local] is not null && _localBattles.Add(battle))
            {
                Sound.Play(SoundIds.BattleStations);
                Sound.BattleMusic(true);
            }
        }

        foreach (var ended in _localBattles.Where(b => !battles.Contains(b)).ToList())
        {
            _localBattles.Remove(ended);
            Sound.BattleMusic(false);
        }
    }

    /// <summary>
    /// What a tick means for the screens: the end of the game
    /// or of the local player plays its movie and leaves the game; another
    /// player's elimination plays its movie.
    /// </summary>
    internal void GameEvents(StepEvents events)
    {
        // every 4000 ticks, when the preference is on.
        // a treaty offered to the local player.
        if ((events & StepEvents.TreatyOffered) != 0)
        {
            Sound.Play(SoundIds.TreatyOffered);
        }

        if ((events & StepEvents.Autosave) != 0 && Preferences.Flag(Preferences.AutoSaveGame))
        {
            SaveGame(AutoSaveName);
        }

        // the end window first, then its movie over it.
        if ((events & (StepEvents.Victory | StepEvents.Defeat)) != 0)
        {
            Switch(ScreenMode.Victory);
            Request(new HostRequest(HostRequestKind.PlayMovie, (events & StepEvents.Victory) != 0 ? "PlayerVictory.mp4" : "DefeatMovie.mp4"));
        }
        else if ((events & StepEvents.PlayerEliminated) != 0)
        {
            Request(new HostRequest(HostRequestKind.PlayMovie, "OtherPlayerElim.mp4"));
        }
    }

    public void Draw(Framebuffer target)
    {
        Current.Window.Draw(target);
        Popup?.Window.Draw(target);
        Console.Draw(target, Assets);
        if (Pointer is { } p && !CursorOnCanvas)
        {
            Assets.Cursor.Draw(target, Assets, p.X, p.Y, _now);
        }
    }
}
