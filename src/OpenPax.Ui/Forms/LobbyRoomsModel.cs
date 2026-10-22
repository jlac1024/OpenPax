using System.Collections.ObjectModel;
using System.Globalization;
using OpenPax.Forms;
using OpenPax.Net.Lobby;
using OpenPax.Ui.Screens;

namespace OpenPax.Ui.Forms;

/// <summary>The rooms form's view model and code-behind.</summary>
public sealed class LobbyRoomsModel : Observable
{
    private readonly UiApp _app;

    public LobbyRoomsModel(UiApp app)
    {
        _app = app;
        Chat = LobbyFeed.Chat(app);
        Refresh();
    }

    public string Title => UiText.RoomsTitle + "  -  " + _app.PlayerName;

    public ObservableCollection<RoomItem> Rooms { get; } = [];

    public ObservableCollection<string> Chat { get; }

    public int Picked
    {
        get;
        set
        {
            if (Set(ref field, value))
            {
                Describe();
            }
        }
    } = -1;

    public string Details
    {
        get;
        private set => Set(ref field, value);
    } = "";

    public bool CanJoin
    {
        get;
        private set => Set(ref field, value);
    }

    public string Password
    {
        get;
        set => Set(ref field, value);
    } = "";

    public string Say
    {
        get;
        set => Set(ref field, value);
    } = "";

    public string Status
    {
        get;
        private set => Set(ref field, value);
    } = "";

    private LobbyClient? Lobby => _app.Lobby;

    private RoomInfo? PickedRoom => Picked >= 0 && Picked < Rooms.Count ? Rooms[Picked].Room : null;

    public void OnNewRoom() => Lobby?.CreateRoom(_app.PlayerName + UiText.RoomNameSuffix, Password, LobbyServer.MaxPlayers);

    public void OnJoin()
    {
        if (PickedRoom is { InGame: false } room)
        {
            Lobby?.JoinRoom(room.Id, Password);
        }
    }

    public void OnSay()
    {
        if (Lobby is { } lobby)
        {
            LobbyFeed.Say(lobby, Say);
        }

        Say = "";
    }

    public void OnLeave()
    {
        Lobby?.Dispose();
        _app.Lobby = null;
        _app.OpenMultiplayerMenu = true;
        _app.Switch(ScreenMode.GameStart);
    }

    public void Tick()
    {
        if (LobbyFeed.Pump(_app, Chat) is not true)
        {
            return;
        }

        if (Lobby?.Room is not null)
        {
            _app.Switch(ScreenMode.LobbyRoom);
            return;
        }

        Refresh();
    }

    private void Refresh()
    {
        if (Lobby is not { } lobby)
        {
            return;
        }

        var keep = PickedRoom?.Id;
        Rooms.Clear();
        foreach (var room in lobby.Rooms)
        {
            Rooms.Add(new RoomItem(room));
        }

        Picked = keep is null ? -1 : Rooms.ToList().FindIndex(r => r.Room.Id == keep);
        if (lobby.Refusal is { } refusal)
        {
            Status = refusal;
            lobby.Refusal = null;
        }

        Describe();
    }

    private void Describe()
    {
        var room = PickedRoom;
        CanJoin = room is { InGame: false };
        Details = room is null ? "" : string.Format(CultureInfo.InvariantCulture, "{0}{1}, {2} {3}/{4}, {5} {6}. {7} {8}",
            UiText.RoomHost, room.Host, UiText.RoomPlayers, room.Players, room.MaxPlayers, UiText.RoomLocked, room.Locked ? UiText.Yes : UiText.No,
            UiText.RoomModsLabel, ModNames(room.Mods));
    }

    /// <summary>A room's mods by name where this player has them, else by id, with their versions; "none" for none.</summary>
    private string ModNames(string set)
    {
        var names = set.Split(',', StringSplitOptions.RemoveEmptyEntries).Select(key =>
        {
            var (id, version) = (key.Split('@')[0], key.Split('@').ElementAtOrDefault(1)?.Split('#')[0] ?? "");
            return (_app.AvailableMods.FirstOrDefault(m => m.Id == id)?.Name ?? id) + " " + version;
        }).ToList();
        return names.Count == 0 ? UiText.RoomModsNone : string.Join(", ", names);
    }
}
