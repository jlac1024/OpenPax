using System.Collections.ObjectModel;
using System.Net.Sockets;
using OpenPax.Forms;
using OpenPax.Forms.Layout;
using OpenPax.Net.Lan;
using OpenPax.Net.Lobby;
using OpenPax.Ui.Screens;

namespace OpenPax.Ui.Forms;

/// <summary>
/// The MULTIPLAYER form's view model and code-behind (docs/ui/lobby.md):
/// the page for the lobby choice made on Game Start, with the address (or
/// the lobbies found on the LAN) and the name.
/// </summary>
public sealed class LobbyConnectModel : Observable, IDisposable
{
    /// <summary>How often the LAN is asked again while its page is open, in calls to <see cref="Tick"/> (the screen ticks about 50 times a second).</summary>
    private const int ScanEvery = 100;

    private readonly UiApp _app;
    private LanScan? _scan;
    private int _sinceScan;
    private string _foundKey = "";

    public LobbyConnectModel(UiApp app)
    {
        _app = app;
        Name = app.PlayerName;
        Status = app.LobbyStatus ?? "";
        Choice = app.LobbyChoice == LobbyChoice.None ? LobbyChoice.Private : app.LobbyChoice;
    }

    public LobbyChoice Choice
    {
        get;
        private set
        {
            field = value;
            _app.LobbyChoice = value;
            Address = value switch
            {
                LobbyChoice.Private => _app.LobbyAddress,
                _ => "",
            };
            if (Status.Length == 0 || _app.LobbyStatus is null)
            {
                Status = value switch
                {
                    LobbyChoice.Lan => UiText.LobbyLanHelp,
                    LobbyChoice.Public => UiText.LobbyPublicHelp,
                    LobbyChoice.Private => UiText.LobbyPrivateHelp,
                    _ => "",
                };
            }

            _app.LobbyStatus = null;
            if (value == LobbyChoice.Lan)
            {
                StartScan();
            }
            else
            {
                StopScan();
            }

            foreach (var name in new[] { nameof(ListShown), nameof(AddressShown), nameof(PublicShown), nameof(Heading), nameof(AddressLabel) })
            {
                Changed(name);
            }
        }
    }

    public Visibility ListShown => Choice == LobbyChoice.Lan ? Visibility.Visible : Visibility.Collapsed;

    /// <summary>Only a private lobby's address is typed; the public one comes from config.json.</summary>
    public Visibility AddressShown => Choice == LobbyChoice.Private ? Visibility.Visible : Visibility.Collapsed;

    public Visibility PublicShown => Choice == LobbyChoice.Public ? Visibility.Visible : Visibility.Collapsed;

    public string PublicLine => UiText.LobbyPublicLine + _app.PublicLobbyAddress;

    public string Heading => Choice switch
    {
        LobbyChoice.Lan => UiText.LobbyLanHeading,
        LobbyChoice.Public => UiText.LobbyPublicHeading,
        _ => UiText.LobbyPrivateHeading,
    };

    public string AddressLabel => UiText.LobbyPrivateLabel;

    /// <summary>The LAN's lobbies as listed; <see cref="FoundLobbies"/> holds what each line stands for.</summary>
    public ObservableCollection<string> Found { get; } = [];

    public List<LanLobby> FoundLobbies { get; } = [];

    public int Picked
    {
        get;
        set => Set(ref field, value);
    } = -1;

    public string Address
    {
        get;
        set => Set(ref field, value);
    } = "";

    /// <summary>The player's name, kept as it is typed (Options shows the same one).</summary>
    public string Name
    {
        get;
        set
        {
            if (Set(ref field, value) && value.Trim().Length > 0)
            {
                _app.PlayerName = value.Trim();
            }
        }
    }

    public string Status
    {
        get;
        set => Set(ref field, value);
    } = "";

    /// <summary>Back to Game Start's multiplayer choices.</summary>
    public void OnBack()
    {
        _app.Lobby?.Dispose();
        _app.Lobby = null;
        _app.OpenMultiplayerMenu = true;
        _app.Switch(ScreenMode.GameStart);
    }

    public void OnConnect()
    {
        string text;
        if (Choice == LobbyChoice.Lan)
        {
            if (Picked < 0 || Picked >= FoundLobbies.Count)
            {
                Status = UiText.LobbyPickOne;
                return;
            }

            text = FoundLobbies[Picked].Address;
        }
        else if (Choice == LobbyChoice.Public)
        {
            text = _app.PublicLobbyAddress;
        }
        else
        {
            text = Address.Trim();
        }

        _app.PlayerName = Name.Trim();
        if (Choice == LobbyChoice.Private)
        {
            _app.LobbyAddress = text;
        }

        if (!LobbyAddress.TryParse(text, out var address))
        {
            Status = UiText.LobbyBadAddress;
            return;
        }

        _app.Lobby?.Dispose();
        _app.Lobby = null;
        Status = UiText.LobbyConnecting;
        try
        {
            _app.Lobby = new LobbyClient(address, _app.PlayerName, _app.ModFingerprint);
        }
        catch (Exception e) when (e is SocketException or TimeoutException or IOException)
        {
            Status = UiText.LobbyCannotReach + e.Message;
        }
    }

    /// <summary>The LAN asked now and then; once the lobby has taken the player in, its rooms; a refusal is shown here.</summary>
    public void Tick()
    {
        if (_scan is not null)
        {
            if (++_sinceScan >= ScanEvery)
            {
                _sinceScan = 0;
                _scan.Send();
            }

            ShowFound(_scan.Found);
        }

        if (_app.Lobby is not { } lobby)
        {
            return;
        }

        lobby.Pump();
        if (lobby.Member > 0)
        {
            _app.LobbyStatus = null;
            Dispose();
            _app.Switch(ScreenMode.LobbyRooms);
        }
        else if (lobby.Refusal is not null || !lobby.Connected)
        {
            Status = lobby.Refusal ?? UiText.LobbyLost;
            lobby.Dispose();
            _app.Lobby = null;
        }
    }

    public void Dispose() => StopScan();

    private void StartScan()
    {
        StopScan();
        try
        {
            _scan = new LanScan(OpenPax.Net.NetProtocol.DefaultLobbyPort, _app.LanHosts, [.. _app.LanScanAlso]);
            _scan.Send();
            _sinceScan = 0;
        }
        catch (SocketException e)
        {
            Status = UiText.LobbyNoScan + e.Message;
        }
    }

    private void StopScan()
    {
        _scan?.Dispose();
        _scan = null;
        FoundLobbies.Clear();
        Found.Clear();
        _foundKey = "";
        Picked = -1;
    }

    private void ShowFound(IReadOnlyList<LanLobby> lobbies)
    {
        // Only a change in the list rebuilds it, so a pick stays put between answers.
        var key = string.Join('\n', lobbies.Select(l => l.Address + " " + l.Name + " " + l.Players));
        if (key == _foundKey)
        {
            return;
        }

        _foundKey = key;
        var kept = Picked >= 0 && Picked < FoundLobbies.Count ? FoundLobbies[Picked].Address : null;
        FoundLobbies.Clear();
        FoundLobbies.AddRange(lobbies);
        Found.Clear();
        foreach (var l in lobbies)
        {
            var version = l.Version == OpenPax.Net.NetProtocol.Version ? "" : UiText.LobbyOtherVersion;
            Found.Add(string.Format(System.Globalization.CultureInfo.InvariantCulture, UiText.LobbyFoundLine, l.Name, l.Address, l.Players, version));
        }

        Picked = kept is null ? (FoundLobbies.Count == 1 ? 0 : -1) : FoundLobbies.FindIndex(l => l.Address == kept);
    }
}
