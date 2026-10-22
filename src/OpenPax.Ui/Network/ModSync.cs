using System.Globalization;
using OpenPax.Net.Lobby;

namespace OpenPax.Ui.Network;

/// <summary>
/// Keeps the player's mods the room host's (docs/modding/multiplayer.md).
/// In another's room the host's set is loaded in place, any mod not here
/// fetched through the lobby first; out of a room, or hosting, the player's
/// own set comes back. The lobby hears each change, and the host may start
/// only once every member's set is its own. Nothing changes while a game runs.
/// </summary>
internal sealed class ModSync
{
    private readonly HashSet<string> _requested = new(StringComparer.Ordinal);
    private string? _tried;
    private bool _arrived;

    /// <summary>What the room screen says of the mods: fetching, missing, or nothing when in step.</summary>
    public string Status { get; private set; } = "";

    public void Tick(UiApp app)
    {
        if (app.Lobby is not { Connected: true } lobby)
        {
            return;
        }

        // As host, the room's members fetch their mods from here, even mid game.
        lobby.PackageFor = app.PackMod;
        if (app.Session is not null || app.LoadMods is null)
        {
            return;
        }

        foreach (var (key, package) in lobby.TakeMods())
        {
            _requested.Remove(key);
            _arrived |= app.InstallMod?.Invoke(key, package) == true;
        }

        // In another's room its host's set; else the player's own.
        var target = lobby.Room is { InGame: false } room && !lobby.IsHost ? room.Mods : null;
        if (target is null)
        {
            Status = "";
            if (app.LoadedModSet is not null)
            {
                app.LoadMods(null);
                lobby.SetMods(app.ModFingerprint);
            }

            return;
        }

        if (target == app.ModFingerprint)
        {
            Status = "";
            if (_tried != target)
            {
                // Already the host's: the lobby is told, once.
                _tried = target;
                lobby.SetMods(app.ModFingerprint);
            }

            return;
        }

        if (_tried == target && !_arrived)
        {
            Status = Progress(lobby);
            return;
        }

        _tried = target;
        _arrived = false;
        var missing = app.LoadMods(target);
        if (missing.Count == 0)
        {
            Status = "";
            lobby.SetMods(app.ModFingerprint);
            return;
        }

        foreach (var key in missing.Where(_requested.Add))
        {
            lobby.RequestMod(key);
        }

        Status = Progress(lobby);
    }

    private string Progress(LobbyClient lobby)
    {
        var downloads = lobby.Downloads;
        if (downloads.Count == 0)
        {
            return _requested.Count == 0 ? "" : UiText.ModsWaiting;
        }

        var (have, total) = (downloads.Sum(d => d.Have), downloads.Sum(d => d.Total));
        return string.Format(CultureInfo.InvariantCulture, UiText.ModsFetching, downloads.Count, have / 1024, Math.Max(have, total) / 1024);
    }
}
