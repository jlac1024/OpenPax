using System.Collections.ObjectModel;
using System.Globalization;
using OpenPax.Forms;
using OpenPax.Forms.Layout;
using OpenPax.Net.Client;
using OpenPax.Net.Lobby;
using OpenPax.Ui.Network;
using OpenPax.Ui.Screens;
using OpenPax.Ui.Widgets;

namespace OpenPax.Ui.Forms;

/// <summary>
/// The room form's view model and code-behind: the members and their
/// choices, this member's own choices, the host's galaxy, the chat, and
/// starting the game. When it starts the host's game is made and served;
/// a member's arrives through the relay (docs/ui/lobby.md).
/// </summary>
public sealed class LobbyRoomModel : Observable
{
    private readonly UiApp _app;
    private readonly IReadOnlyList<Data.Tables.RaceTemplate> _stock;
    private readonly IReadOnlyList<Data.Tables.RaceTemplate> _designed;
    private bool _refreshing;
    private bool _starting;
    private GameClient? _client;

    public LobbyRoomModel(UiApp app)
    {
        _app = app;
        _stock = app.Assets.LoadStockSpecies();
        _designed = [.. app.CustomSpecies];

        // The stock species, then the player's designed ones, sent whole with the choice.
        Species = [.. _stock.Select(s => s.Name), .. _designed.Select(s => s.Name + UiText.RoomDesignedMark)];
        _app.LobbyChat.Clear();
        Chat = [];

        // The seat starts on the player's Options, sent once the room is shown.
        _refreshing = true;
        SpeciesIndex = PreferredSpecies;
        Flag = (app.Preferences.Get(Preferences.FlagId) % FlagPane.Flags) + 1;
        Color = (app.Preferences.Get(Preferences.FleetId) % FleetColorPane.Colors) + 1;
        _refreshing = false;
        Refresh();
        Choose();
    }

    private int PreferredSpecies => Math.Clamp(_app.Preferences.Get(Preferences.PreferredSpecies), 0, _stock.Count - 1);

    public string Title
    {
        get;
        private set => Set(ref field, value);
    } = "";

    public ObservableCollection<string> Members { get; } = [];

    public ObservableCollection<string> Chat { get; }

    public IReadOnlyList<string> Species { get; }

    public bool IsHost
    {
        get;
        private set => Set(ref field, value);
    }

    public Visibility HostOnly => IsHost ? Visibility.Visible : Visibility.Collapsed;

    public Visibility MemberOnly => IsHost ? Visibility.Collapsed : Visibility.Visible;

    public bool CanStart
    {
        get;
        private set => Set(ref field, value);
    }

    public string Status
    {
        get;
        private set => Set(ref field, value);
    } = "";

    public string Say
    {
        get;
        set => Set(ref field, value);
    } = "";

    public bool Ready
    {
        get;
        set
        {
            if (Set(ref field, value) && !_refreshing && !_starting)
            {
                _app.Lobby?.SetReady(value);
            }
        }
    }

    public int SpeciesIndex
    {
        get;
        set
        {
            if (Set(ref field, value))
            {
                Choose();
            }
        }
    }

    /// <summary>1 to 30, as Game Setup counts them.</summary>
    public int Flag
    {
        get;
        set
        {
            if (Set(ref field, value))
            {
                Changed(nameof(FlagImage));
                Choose();
            }
        }
    } = 1;

    public string FlagImage => "Empire:" + (Flag - 1 + 1000).ToString(CultureInfo.InvariantCulture);

    /// <summary>1 to 16: the fleet, whose ships each have their own look.</summary>
    public int Color
    {
        get;
        set
        {
            if (Set(ref field, value))
            {
                Changed(nameof(ShipSmall));
                Changed(nameof(ShipMedium));
                Choose();
            }
        }
    } = 1;

    // Two of the fleet's hulls, as Options and Game Setup's fleet panel show them.
    public string ShipSmall => "Combat:" + (((Color - 1) * 1000) + 1101).ToString(CultureInfo.InvariantCulture);

    public string ShipMedium => "Combat:" + (((Color - 1) * 1000) + 1301).ToString(CultureInfo.InvariantCulture);

    public int Stars
    {
        get;
        set
        {
            if (Set(ref field, value))
            {
                SendSettings();
            }
        }
    } = RoomSettings.Default.Stars;

    public int Opponents
    {
        get;
        set
        {
            if (Set(ref field, value))
            {
                SendSettings();
            }
        }
    } = RoomSettings.Default.Opponents;

    public int Difficulty
    {
        get;
        set
        {
            if (Set(ref field, value))
            {
                SendSettings();
            }
        }
    } = RoomSettings.Default.Difficulty;

    private LobbyClient? Lobby => _app.Lobby;

    /// <summary>
    /// The room's mods (docs/modding/multiplayer.md). For the host, every mod
    /// found, each on or off for the room; for a member, the host's in order,
    /// each found here or still to fetch.
    /// </summary>
    public ObservableCollection<string> Mods { get; } = [];

    public int ModIndex
    {
        get;
        set => Set(ref field, value);
    } = -1;

    public string ModStatus
    {
        get;
        private set => Set(ref field, value);
    } = "";

    // The host's list, in the order shown: the mods on first, in their order, then the rest by name.
    private List<ModListing> _modOrder = [];

    /// <summary>The host turns the picked mod on or off for the room: loaded in place at once, and the members follow.</summary>
    public void OnModToggle()
    {
        if (!IsHost || _starting || ModIndex < 0 || ModIndex >= _modOrder.Count)
        {
            return;
        }

        var id = _modOrder[ModIndex].Id;
        if (!_app.EnabledMods.Remove(id))
        {
            _app.EnabledMods.Add(id);
        }

        ReloadOwn();
    }

    /// <summary>The host moves the picked mod up or down the load order.</summary>
    public void OnModUp() => MoveMod(-1);

    public void OnModDown() => MoveMod(1);

    private void MoveMod(int by)
    {
        if (!IsHost || _starting || ModIndex < 0 || ModIndex >= _modOrder.Count || _app.EnabledMods.IndexOf(_modOrder[ModIndex].Id) is var at && at < 0)
        {
            return;
        }

        var to = Math.Clamp(at + by, 0, _app.EnabledMods.Count - 1);
        var id = _app.EnabledMods[at];
        _app.EnabledMods.RemoveAt(at);
        _app.EnabledMods.Insert(to, id);
        ReloadOwn();
    }

    // The host's own set loaded in place; the screen is made again with it, and the lobby told.
    private void ReloadOwn()
    {
        if (_app.LoadMods is null || Lobby is not { } lobby)
        {
            return;
        }

        _app.LoadMods(null);
        lobby.SetMods(_app.ModFingerprint);
    }

    private void RefreshMods(LobbyClient lobby, RoomInfo room)
    {
        Mods.Clear();
        if (IsHost)
        {
            var on = _app.EnabledMods.Select(id => _app.AvailableMods.FirstOrDefault(m => m.Id == id)).OfType<ModListing>();
            _modOrder = [.. on, .. _app.AvailableMods.Where(m => !_app.EnabledMods.Contains(m.Id)).OrderBy(m => m.Name, StringComparer.OrdinalIgnoreCase)];
            foreach (var m in _modOrder)
            {
                Mods.Add((_app.EnabledMods.Contains(m.Id) ? UiText.ModOn : UiText.ModOff) + m.Name + " " + m.Version);
            }

            ModStatus = _modOrder.Count == 0 ? UiText.OptionsNoMods : UiText.RoomModsHost;
            return;
        }

        var loaded = _app.ModFingerprint.Split(',', StringSplitOptions.RemoveEmptyEntries).ToHashSet(StringComparer.Ordinal);
        foreach (var key in room.Mods.Split(',', StringSplitOptions.RemoveEmptyEntries))
        {
            var id = key.Split('@')[0];
            var name = _app.AvailableMods.FirstOrDefault(m => m.Id == id)?.Name ?? id;
            Mods.Add(name + " " + key.Split('@').ElementAtOrDefault(1)?.Split('#')[0] + (loaded.Contains(key) ? "" : UiText.RoomModFetching));
        }

        ModStatus = _app.ModSync.Status is { Length: > 0 } fetching ? fetching
            : room.Mods.Length == 0 ? UiText.RoomNoMods
            : UiText.RoomModsInStep;
    }

    public void OnSay()
    {
        if (Lobby is { } lobby)
        {
            LobbyFeed.Say(lobby, Say);
        }

        Say = "";
    }

    public void OnStart()
    {
        if (!_starting)
        {
            Lobby?.StartGame();
        }
    }

    public void OnLeave()
    {
        if (_starting)
        {
            return;
        }

        Lobby?.LeaveRoom();
        _app.LobbyChat.Clear();
        _app.Switch(ScreenMode.LobbyRooms);
    }

    public void Tick(long now)
    {
        var pumped = LobbyFeed.Pump(_app, Chat);
        if (pumped is null || Lobby is not { } lobby)
        {
            return;
        }

        if (lobby.Room is null)
        {
            _app.Switch(ScreenMode.LobbyRooms);
            return;
        }

        // The mods' progress is shown as it comes, not only when the room changes.
        if (pumped == true || (!IsHost && ModStatus != _app.ModSync.Status && _app.ModSync.Status.Length > 0))
        {
            Refresh();
        }

        if (lobby.Starting is { } starting && !_starting)
        {
            _starting = true;
            Status = UiText.RoomStarting;
            if (lobby.IsHost)
            {
                _app.Request(new HostRequest(HostRequestKind.StartNetworkGame, Network: Choices(lobby)));
            }
            else if (_app.Tables?.Invoke() is { } tables)
            {
                _client = new GameClient(lobby.OpenRelay(starting.Tickets[0], host: false), tables, _app.PlayerName);
            }
        }

        // A member's game is played once the first whole state is in.
        if (_client is { Game: { } game, Player: >= 0 } client)
        {
            _client = null;
            _app.NetworkGameStarted(game, game.Players[client.Player], new ClientLink(client), now);
        }
        else
        {
            _client?.Pump();
        }
    }

    /// <summary>Left before the game came: the half made client goes.</summary>
    public void Closed() => _client?.Dispose();

    /// <summary>
    /// The game to make: the room's galaxy, a clock-picked seed as Game
    /// Setup picks one, and the members in seat order as they chose (a
    /// designed species as sent), a choice not made being the stock species
    /// in turn and the seat's flag and color.
    /// </summary>
    private NetworkChoices Choices(LobbyClient lobby)
    {
        var seats = lobby.Members.Select((m, i) => new NetworkSeat(
            m.Name,
            m.Choice.Flag >= 0 ? m.Choice.Flag % FlagPane.Flags : i,
            m.Choice.Color >= 0 ? m.Choice.Color % FleetColorPane.Colors : i,
            SeatSpecies.For(m.Choice, i, _stock, _app.Assets.Strings))).ToList();
        var settings = lobby.Room?.Settings ?? RoomSettings.Default;
        var seed = (uint)(DateTime.Now.Ticks / TimeSpan.TicksPerMillisecond % 25000);
        return new NetworkChoices(seed, settings.Stars, settings.Opponents, settings.Difficulty, seats);
    }

    private void Choose()
    {
        if (!_refreshing && !_starting)
        {
            var designed = SpeciesIndex >= _stock.Count && SpeciesIndex - _stock.Count < _designed.Count ? _designed[SpeciesIndex - _stock.Count] : null;
            Lobby?.Choose(designed is null
                ? new SeatChoice(SpeciesIndex, Flag - 1, Color - 1)
                : new SeatChoice(-1, Flag - 1, Color - 1, SeatSpecies.Pack(designed)));
        }
    }

    private void SendSettings()
    {
        if (!_refreshing && IsHost)
        {
            Lobby?.SetSettings(new RoomSettings(Stars, Opponents, Difficulty));
        }
    }

    private void Refresh()
    {
        if (Lobby is not { Room: { } room } lobby)
        {
            return;
        }

        _refreshing = true;
        try
        {
            Title = room.Name;
            IsHost = lobby.IsHost;
            Changed(nameof(HostOnly));
            Changed(nameof(MemberOnly));
            Members.Clear();
            for (var i = 0; i < lobby.Members.Count; i++)
            {
                var m = lobby.Members[i];
                var species = SeatSpecies.For(m.Choice, i, _stock, _app.Assets.Strings).Name;
                var syncing = i > 0 && m.Mods != lobby.Members[0].Mods ? UiText.RoomSyncingMark : "";
                Members.Add(m.Name + (i == 0 ? UiText.RoomHostMark : m.Ready ? UiText.RoomReadyMark : "") + " - " + species + syncing);
            }

            (Stars, Opponents, Difficulty) = (room.Settings.Stars, room.Settings.Opponents, room.Settings.Difficulty);
            if (lobby.Members.FirstOrDefault(m => m.Member == lobby.Member) is { } me)
            {
                Ready = me.Ready;
                if (me.Choice is { Species: < 0, Custom: null })
                {
                    SpeciesIndex = PreferredSpecies;
                }
            }

            RefreshMods(lobby, room);
            var allReady = lobby.Members.Count > 1 && lobby.Members.Skip(1).All(m => m.Ready && m.Mods == lobby.Members[0].Mods);
            CanStart = IsHost && allReady && !_starting;
            Status = lobby.Refusal ?? (_starting ? UiText.RoomStarting : IsHost ? (allReady ? UiText.RoomAllReady : UiText.RoomWaiting) : UiText.RoomMemberHelp);
            lobby.Refusal = null;
        }
        finally
        {
            _refreshing = false;
        }
    }
}
