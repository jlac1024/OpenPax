using OpenPax.Forms;
using OpenPax.Forms.Controls;
using OpenPax.Net.Lobby;
using OpenPax.Render;
using OpenPax.Render.TrueColor;
using OpenPax.Sim.Setup;
using OpenPax.Ui.Forms;
using OpenPax.Ui.Network;
using OpenPax.Ui.Screens;
using static OpenPax.Ui.Tests.UiGames;

namespace OpenPax.Ui.Tests;

/// <summary>Two apps meet in a lobby through its forms and start a game together.</summary>
public sealed class LobbyScreenTests : IDisposable
{
    private readonly LobbyServer _lobby = new();
    private readonly int _port;
    private readonly UiApp _ann = NewApp();
    private readonly UiApp _bo = NewApp();
    private readonly Canvas _canvas = new(640, 480);
    private long _now;

    public LobbyScreenTests()
    {
        _port = _lobby.Listen(0);
        _ann.Tables = Tables;
        _bo.Tables = Tables;
    }

    public void Dispose()
    {
        _ann.Lobby?.Dispose();
        _bo.Lobby?.Dispose();
        _lobby.Dispose();
    }

    private void Frame()
    {
        _now += 20;
        _lobby.Tick(_now);
        Thread.Sleep(2);
        foreach (var request in _ann.Tick(_now).ToList())
        {
            // What the host program does with a hosted network game.
            if (request is { Kind: HostRequestKind.StartNetworkGame, Network: { } n })
            {
                var humans = n.Humans.Select(h => new HumanPlayerSetup(h.Name, h.Flag, h.FleetColor, OpenPax.Host.Headless.GameTablesLoader.ToSpecies(h.Species))).ToList();
                _ann.HostedGameReady(NewGame.Create(new GameSetup(n.Seed, n.Stars, n.Opponents + humans.Count, n.Difficulty, humans), Tables()), _now);
            }
        }

        _ = _bo.Tick(_now).ToList();
        _ann.DrawCanvas(_canvas, ClassicPlacement.For(640, 480, 640, 480));
        _bo.DrawCanvas(_canvas, ClassicPlacement.For(640, 480, 640, 480));
    }

    private void Until(Func<bool> done, string what)
    {
        for (var i = 0; i < 2000 && !done(); i++)
        {
            Frame();
        }

        Assert.True(done(), what);
    }

    private static Form FormOf(UiApp app) => ((FormScreen)app.Current).Form;

    private static T Get<T>(UiApp app, string name)
        where T : Element => FormOf(app).Get<T>(name);

    private void Click(UiApp app, Element e, int dy = -1)
    {
        var (x, y) = (e.Bounds.X + Math.Min(e.Bounds.Width / 2, 20), dy < 0 ? e.Bounds.Y + e.Bounds.Height / 2 : e.Bounds.Y + dy);
        app.FormsMouseMove(x, y);
        Assert.True(app.FormsMouseDown(x, y, false), $"{e.Name} takes the press");
        app.FormsMouseUp(x, y);
        Frame();
    }

    private void Type(UiApp app, string field, string text, bool submit = false)
    {
        var box = Get<TextBox>(app, field);
        Click(app, box);
        box.Text = "";
        app.Typed(text, _now);
        if (submit)
        {
            app.Key(UiKey.Return, false, _now);
        }
    }

    /// <summary>
    /// Game Start's Multiplayer Game, then one of the choices that take the
    /// main buttons' places: LAN first, then the public lobby when one is
    /// named, then a private lobby.
    /// </summary>
    private void Choose(UiApp app, LobbyChoice choice)
    {
        Assert.Equal(ScreenMode.GameStart, app.Mode);
        ClickButton(app, 31101);
        Assert.Equal(ScreenMode.GameStart, app.Mode);
        var publicShown = app.PublicLobbyAddress is not null ? 1 : 0;
        ClickButton(app, choice switch
        {
            LobbyChoice.Lan => 31100,
            LobbyChoice.Public => 31101,
            _ => 31101 + publicShown,
        });
        Assert.Equal(ScreenMode.LobbyConnect, app.Mode);
        Frame();
    }

    private void ClickButton(UiApp app, int id)
    {
        var b = app.Current.Window.Get(id);
        UiGames.Click(app, b.Left + 5, b.Top + 5, _now);
    }

    private void Connect(UiApp app, string name)
    {
        Choose(app, LobbyChoice.Private);
        Type(app, "Address", "127.0.0.1:" + _port);
        Type(app, "PlayerName", name);
        Click(app, Get<Button>(app, "Connect"));
        Until(() => app.Mode == ScreenMode.LobbyRooms, name + " reaches the rooms");
    }

    [Fact]
    public void TwoPlayersMeetChooseChatAndStartAGame()
    {
        Connect(_ann, "Ann");
        Connect(_bo, "Bo");

        Click(_ann, Get<Button>(_ann, "NewRoom"));
        Until(() => _ann.Mode == ScreenMode.LobbyRoom, "Ann opens a room");
        Until(() => Get<ListBox>(_bo, "Rooms").Items.Count == 1, "Bo sees it");
        Click(_bo, Get<ListBox>(_bo, "Rooms"), 6);
        Click(_bo, Get<Button>(_bo, "Join"));
        Until(() => _bo.Mode == ScreenMode.LobbyRoom, "Bo joins");
        Until(() => Get<ListBox>(_ann, "Members").Items.Count == 2, "Ann sees Bo");

        // Chat typed in the room reaches the other.
        Type(_bo, "Say", "hello Ann", submit: true);
        Until(() => Get<ListBox>(_ann, "Chat").Items.Contains("Bo: hello Ann"), "the room hears Bo");

        // Bo picks a species and flag; Ann sees the species; Ann's galaxy reaches Bo.
        var model = (LobbyRoomModel)FormOf(_bo).CodeBehind!; // The screen's model is the form's code-behind.
        model.SpeciesIndex = 3;
        model.Flag = 7;
        var species = _bo.Assets.LoadStockSpecies()[3].Name;
        Until(() => Get<ListBox>(_ann, "Members").Items.Any(i => i.ToString()!.Contains(species, StringComparison.Ordinal)), "Ann sees Bo's species");
        ((LobbyRoomModel)FormOf(_ann).CodeBehind!).Stars = 45; // As above.
        Until(() => Get<NumericUpDown>(_bo, "Stars").Value == 45, "Bo sees the stars");
        Assert.False(Get<NumericUpDown>(_bo, "Stars").IsEnabled);

        Assert.False(Get<Button>(_ann, "Start").IsEnabled);
        Click(_bo, Get<CheckBox>(_bo, "Ready"));
        Until(() => Get<Button>(_ann, "Start").IsEnabled, "START opens once Bo is ready");
        Click(_ann, Get<Button>(_ann, "Start"));

        Until(() => _ann.Mode == ScreenMode.MainControl && _bo.Mode == ScreenMode.MainControl, "both are in the game");
        Assert.Null(_ann.Forms.Top);
        Assert.IsType<HostLink>(_ann.Session!.Link);
        Assert.IsType<ClientLink>(_bo.Session!.Link);
        var bo = _bo.Session.Local;
        Assert.Equal("Bo", bo.Name);
        Assert.Equal(species, bo.Species.Name);
        Assert.Equal(6, bo.Flag);
        Assert.Equal(45, _bo.Session.Game.Universe.Stars.Count);
        var server = ((HostLink)_ann.Session.Link).Server;
        server.Clock.Paused = true;
        Until(() => _bo.Session.Game.Hash() == _ann.Session.Game.Hash(), "Bo's game matches Ann's");
    }

    [Fact]
    public void AJoinerTakesTheHostsModsAndTheStartWaitsUntilTheyHaveThem()
    {
        const string Key = "test-mod@1#00112233aabb";
        var package = new byte[300_000];
        _ann.ModFingerprint = Key;
        _ann.PackMod = key => key == Key ? package : null;

        // Bo's host program, as far as the room needs it: mods are found once installed.
        var installed = new Dictionary<string, byte[]>();
        _bo.InstallMod = (key, bytes) =>
        {
            installed[key] = bytes;
            return true;
        };
        _bo.LoadMods = set =>
        {
            var keys = (set ?? "").Split(',', StringSplitOptions.RemoveEmptyEntries);
            var missing = keys.Where(k => !installed.ContainsKey(k)).ToList();
            if (missing.Count == 0)
            {
                (_bo.ModFingerprint, _bo.LoadedModSet) = (set ?? "", set);
            }

            return missing;
        };

        Connect(_ann, "Ann");
        Connect(_bo, "Bo");
        Click(_ann, Get<Button>(_ann, "NewRoom"));
        Until(() => _ann.Mode == ScreenMode.LobbyRoom, "Ann opens a room");
        Until(() => Get<ListBox>(_bo, "Rooms").Items.Count == 1, "Bo sees it");
        Click(_bo, Get<ListBox>(_bo, "Rooms"), 6);
        Click(_bo, Get<Button>(_bo, "Join"));
        Until(() => _bo.Mode == ScreenMode.LobbyRoom, "Bo joins, though his mods differ");

        Until(() => installed.ContainsKey(Key), "Bo fetches Ann's mod through the lobby");
        Assert.Equal(package, installed[Key]);
        Until(() => _bo.ModFingerprint == Key && _bo.LoadedModSet == Key, "and loads it");
        Until(() => Get<ListBox>(_bo, "Mods").Items.Count == 1 && !Get<ListBox>(_bo, "Mods").Items[0].ToString()!.Contains("fetching", StringComparison.Ordinal), "Bo sees the room's mod as his");
        Until(() => _ann.Lobby!.Members.Count == 2 && _ann.Lobby.Members[1].Mods == Key, "the lobby hears Bo has it");

        Click(_bo, Get<CheckBox>(_bo, "Ready"));
        Until(() => Get<Button>(_ann, "Start").IsEnabled, "START opens once Bo is ready and in step");

        // Back out of the room, Bo's own mods come back.
        Click(_bo, Get<Button>(_bo, "Leave"));
        Until(() => _bo.Mode == ScreenMode.LobbyRooms && _bo.LoadedModSet is null && _bo.ModFingerprint == "", "Bo's own mods again");
    }

    [Fact]
    public void ADesignedSpeciesIsPlayedAsSent()
    {
        var stock = _bo.Assets.LoadStockSpecies();
        var designed = stock[2] with { Name = "Zorgons", Picture = (stock[2].Picture + 1) % 18 };
        var kept = SeatSpecies.Unpack(SeatSpecies.Pack(designed), _bo.Assets.Strings);
        Assert.Equal(("Zorgons", designed.Picture), (kept?.Name, kept?.Picture));
        _bo.CustomSpecies.Add(designed);
        Connect(_ann, "Ann");
        Connect(_bo, "Bo");
        Click(_ann, Get<Button>(_ann, "NewRoom"));
        Until(() => _ann.Mode == ScreenMode.LobbyRoom, "Ann opens a room");
        Until(() => Get<ListBox>(_bo, "Rooms").Items.Count == 1, "Bo sees it");
        Click(_bo, Get<ListBox>(_bo, "Rooms"), 6);
        Click(_bo, Get<Button>(_bo, "Join"));
        Until(() => _bo.Mode == ScreenMode.LobbyRoom, "Bo joins");

        // Bo's designed species is listed after the stock ones; Ann sees it by name.
        var model = (LobbyRoomModel)FormOf(_bo).CodeBehind!; // The screen's model is the form's code-behind.
        Assert.Equal("Zorgons" + UiText.RoomDesignedMark, model.Species[stock.Count]);
        model.SpeciesIndex = stock.Count;
        Until(() => Get<ListBox>(_ann, "Members").Items.Any(i => i.ToString()!.Contains("Zorgons", StringComparison.Ordinal)), "Ann sees Bo's designed species");

        Click(_bo, Get<CheckBox>(_bo, "Ready"));
        Until(() => Get<Button>(_ann, "Start").IsEnabled, "START opens");
        Click(_ann, Get<Button>(_ann, "Start"));
        Until(() => _ann.Mode == ScreenMode.MainControl && _bo.Mode == ScreenMode.MainControl, "both are in the game");
        var hostsBo = _ann.Session!.Game.Players.First(p => p.Name == "Bo");
        Assert.Equal("Zorgons", hostsBo.Species.Name);
        Assert.Equal(designed.Picture, hostsBo.Species.Picture);
        Assert.Equal("Zorgons", _bo.Session!.Local.Species.Name);
    }

    [Fact]
    public void ALobbyOnTheLanIsFoundAndJoined()
    {
        _ann.LanScanAlso.Add(new System.Net.IPEndPoint(System.Net.IPAddress.Loopback, _port));
        Choose(_ann, LobbyChoice.Lan);
        var model = (LobbyConnectModel)FormOf(_ann).CodeBehind!; // The screen's model is the form's code-behind.
        var address = "127.0.0.1:" + _port;

        // Another lobby on the real LAN may answer too; ours is picked by its address.
        Until(() => model.FoundLobbies.Any(l => l.Address == address), "the lobby answers the scan");
        model.Picked = model.FoundLobbies.FindIndex(l => l.Address == address);
        Type(_ann, "PlayerName", "Ann");
        Click(_ann, Get<Button>(_ann, "Connect"));
        Until(() => _ann.Mode == ScreenMode.LobbyRooms, "Ann reaches the rooms");
    }

    [Fact]
    public void ThePublicPageOffersThePublicLobby()
    {
        _ann.PublicLobbyAddress = "127.0.0.1:" + _port;
        Choose(_ann, LobbyChoice.Public);
        Assert.Equal(OpenPax.Forms.Layout.Visibility.Collapsed, Get<TextBox>(_ann, "Address").Visibility);
        Click(_ann, Get<Button>(_ann, "Connect"));
        Until(() => _ann.Mode == ScreenMode.LobbyRooms, "the public lobby takes Ann in");
    }

    [Fact]
    public void LeavingGoesBackStepByStep()
    {
        Connect(_ann, "Ann");
        Click(_ann, Get<Button>(_ann, "NewRoom"));
        Until(() => _ann.Mode == ScreenMode.LobbyRoom, "a room");
        Click(_ann, Get<Button>(_ann, "Leave"));
        Until(() => _ann.Mode == ScreenMode.LobbyRooms, "left the room");
        Click(_ann, Get<Button>(_ann, "Leave"));
        Assert.Equal(ScreenMode.GameStart, _ann.Mode);
        Assert.Null(_ann.Lobby);
        Assert.Null(_ann.Forms.Top);

        // Game Start comes back on its multiplayer choices (no public lobby named: Private Lobby second); Previous Menu leaves them.
        Assert.Equal(UiText.MenuPrivateLobby, _ann.Current.Window.Get<OpenPax.Ui.Widgets.ButtonPane>(31101).Caption);
        ClickButton(_ann, 31102);
        Assert.Null(_ann.Current.Window.Get<OpenPax.Ui.Widgets.ButtonPane>(31101).Caption);
        Assert.True(_ann.Current.Window.Get(31104).Visible);
    }

    [Fact]
    public void AnAddressIsHostOrHostAndPortOrAWebSocket()
    {
        Assert.True(LobbyAddress.TryParse("example.org", out var a));
        Assert.Equal(new LobbyAddress("example.org", OpenPax.Net.NetProtocol.DefaultLobbyPort), a);
        Assert.True(LobbyAddress.TryParse("10.0.0.2:5000", out a));
        Assert.Equal(new LobbyAddress("10.0.0.2", 5000), a);
        Assert.True(LobbyAddress.TryParse("wss://lobby.example.org", out a));
        Assert.Equal(("lobby.example.org", 443), (a.Host, a.Port));
        Assert.NotNull(a.WebSocket);
        Assert.True(LobbyAddress.TryParse("ws://10.0.0.2:8080/lobby", out a));
        Assert.Equal("/lobby", a.WebSocket!.AbsolutePath); // Not null: a ws:// address.
        Assert.False(LobbyAddress.TryParse("http://example.org", out _));
        Assert.False(LobbyAddress.TryParse("x:0", out _));
        Assert.False(LobbyAddress.TryParse("", out _));
    }
}
