using System.Collections.ObjectModel;
using System.Globalization;
using OpenPax.Forms;
using OpenPax.Sim;
using OpenPax.Ui.Screens;
using OpenPax.Ui.Widgets;

namespace OpenPax.Ui.Forms;

/// <summary>
/// The Options form's view model and code-behind (docs/ui/options.md): the
/// player's name, flag, fleet and species that new games and lobby rooms
/// start on, and the display settings. Each change is kept at once; the
/// config file is written when the game closes.
/// </summary>
public sealed class OptionsModel(UiApp app) : Observable
{
    private readonly Preferences _prefs = app.Preferences;

    public string Name
    {
        get;
        set
        {
            if (Set(ref field, value) && value.Trim().Length > 0)
            {
                app.PlayerName = value.Trim();
            }
        }
    } = app.PlayerName;

    public IReadOnlyList<string> Species { get; } = [.. app.Assets.LoadStockSpecies().Select(s => s.Name)];

    public int SpeciesIndex
    {
        get => Math.Clamp(_prefs.Get(Preferences.PreferredSpecies), 0, Math.Max(0, Species.Count - 1));
        set => Keep(Preferences.PreferredSpecies, value);
    }

    /// <summary>1 to 30, as Game Setup and the lobby room count them.</summary>
    public int Flag
    {
        get => (_prefs.Get(Preferences.FlagId) % FlagPane.Flags) + 1;
        set
        {
            Keep(Preferences.FlagId, value - 1);
            Changed(nameof(FlagImage));
        }
    }

    public string FlagImage => "Empire:" + (3000 + Flag - 1).ToString(CultureInfo.InvariantCulture);

    /// <summary>1 to 16; each fleet's ships have their own look.</summary>
    public int Fleet
    {
        get => (_prefs.Get(Preferences.FleetId) % FleetColorPane.Colors) + 1;
        set
        {
            Keep(Preferences.FleetId, value - 1);
            Changed(nameof(ShipSmall));
            Changed(nameof(ShipMedium));
        }
    }

    // Two of the fleet's hulls from those Game Setup's fleet panel shows; its largest does not fit the panel.
    public string ShipSmall => Ship(1101);

    public string ShipMedium => Ship(1301);

    public bool Fullscreen
    {
        get => _prefs.Flag(Preferences.Fullscreen);
        set => Keep(Preferences.Fullscreen, value ? 1 : 0);
    }

    public bool UpscaledArt
    {
        get => _prefs.Flag(Preferences.UpscaledArt);
        set => Keep(Preferences.UpscaledArt, value ? 1 : 0);
    }

    public bool MovingStars
    {
        get => _prefs.Flag(Preferences.MovingStars);
        set => Keep(Preferences.MovingStars, value ? 1 : 0);
    }

    public bool FixComputerKeepsShips
    {
        get => _prefs.Flag(Preferences.FixNames[Fixes.ComputerKeepsShips]);
        set => Keep(Preferences.FixNames[Fixes.ComputerKeepsShips], value ? 1 : 0);
    }

    public bool FixScrapUnderWork
    {
        get => _prefs.Flag(Preferences.FixNames[Fixes.ScrapUnderWork]);
        set => Keep(Preferences.FixNames[Fixes.ScrapUnderWork], value ? 1 : 0);
    }

    public bool FixShipyardOverflow
    {
        get => _prefs.Flag(Preferences.FixNames[Fixes.ShipyardOverflow]);
        set => Keep(Preferences.FixNames[Fixes.ShipyardOverflow], value ? 1 : 0);
    }

    public bool FixFleetsJoinBattles
    {
        get => _prefs.Flag(Preferences.FixNames[Fixes.FleetsJoinBattles]);
        set => Keep(Preferences.FixNames[Fixes.FleetsJoinBattles], value ? 1 : 0);
    }

    public bool FixPlanetBattleSides
    {
        get => _prefs.Flag(Preferences.FixNames[Fixes.PlanetBattleSides]);
        set => Keep(Preferences.FixNames[Fixes.PlanetBattleSides], value ? 1 : 0);
    }

    public bool FixShotsHitPlanets
    {
        get => _prefs.Flag(Preferences.FixNames[Fixes.ShotsHitPlanets]);
        set => Keep(Preferences.FixNames[Fixes.ShotsHitPlanets], value ? 1 : 0);
    }

    public bool FixShotsLeadTrue
    {
        get => _prefs.Flag(Preferences.FixNames[Fixes.ShotsLeadTrue]);
        set => Keep(Preferences.FixNames[Fixes.ShotsLeadTrue], value ? 1 : 0);
    }

    public bool FixExperienceOnce
    {
        get => _prefs.Flag(Preferences.FixNames[Fixes.ExperienceOnce]);
        set => Keep(Preferences.FixNames[Fixes.ExperienceOnce], value ? 1 : 0);
    }

    public bool FixOwnYardRefits
    {
        get => _prefs.Flag(Preferences.FixNames[Fixes.OwnYardRefits]);
        set => Keep(Preferences.FixNames[Fixes.OwnYardRefits], value ? 1 : 0);
    }

    public bool FixColonizeNeedsShip
    {
        get => _prefs.Flag(Preferences.FixNames[Fixes.ColonizeNeedsShip]);
        set => Keep(Preferences.FixNames[Fixes.ColonizeNeedsShip], value ? 1 : 0);
    }

    public bool FixTradeKeepsAllShips
    {
        get => _prefs.Flag(Preferences.FixNames[Fixes.TradeKeepsAllShips]);
        set => Keep(Preferences.FixNames[Fixes.TradeKeepsAllShips], value ? 1 : 0);
    }

    public bool FixStrangersIndifferent
    {
        get => _prefs.Flag(Preferences.FixNames[Fixes.StrangersIndifferent]);
        set => Keep(Preferences.FixNames[Fixes.StrangersIndifferent], value ? 1 : 0);
    }

    public bool ZeroPauses
    {
        get => _prefs.Flag(Preferences.ZeroPauses);
        set => Keep(Preferences.ZeroPauses, value ? 1 : 0);
    }

    public bool VSync
    {
        get => _prefs.Flag(Preferences.VSync);
        set => Keep(Preferences.VSync, value ? 1 : 0);
    }

    public bool MadeSkies
    {
        get => _prefs.Flag(Preferences.MadeSkies);
        set => Keep(Preferences.MadeSkies, value ? 1 : 0);
    }

    /// <summary>0 (still) to 100 (twice the default).</summary>
    public int StarSpeed
    {
        get => Math.Clamp(_prefs.Get(Preferences.StarSpeed), 0, 100);
        set
        {
            Keep(Preferences.StarSpeed, Math.Clamp(value, 0, 100));
            Changed(nameof(StarSpeedText));
        }
    }

    public string StarSpeedText => string.Format(CultureInfo.InvariantCulture, UiText.OptionsStarSpeed, StarSpeed);

    public bool PlayMovies
    {
        get => _prefs.Flag(Preferences.PlayMovies);
        set => Keep(Preferences.PlayMovies, value ? 1 : 0);
    }

    public IReadOnlyList<string> TextFonts { get; } = [UiText.OptionsFontOriginal, UiText.OptionsFontUpscaled, UiText.OptionsFontModern];

    public int TextFont
    {
        get => Math.Clamp(_prefs.Get(Preferences.TextFont), 0, 2);
        set => Keep(Preferences.TextFont, value);
    }

    public IReadOnlyList<string> TextOutlines { get; } = [UiText.OptionsOutlineNone, UiText.OptionsOutlineColors, UiText.OptionsOutlineWhite];

    public int TextOutline
    {
        get => Math.Clamp(_prefs.Get(Preferences.TextOutline), 0, 2);
        set => Keep(Preferences.TextOutline, value);
    }

    /// <summary>The mods folder's mods (docs/modding.md): those turned on first, in their order, then the rest.</summary>
    public ObservableCollection<string> Mods
    {
        get
        {
            if (_mods is null)
            {
                _mods = [];
                ShowMods(null);
            }

            return _mods;
        }
    }

    private ObservableCollection<string>? _mods;
    private List<ModListing> _modOrder = [];

    public int ModIndex
    {
        get;
        set
        {
            if (Set(ref field, value))
            {
                Changed(nameof(ModPicked));
                Changed(nameof(ModDetails));
            }
        }
    } = -1;

    public bool ModPicked => ModIndex >= 0 && ModIndex < _modOrder.Count;

    public string ModDetails => ModPicked
        ? _modOrder[ModIndex].Name + " " + _modOrder[ModIndex].Version + ": " + _modOrder[ModIndex].Description
        : app.AvailableMods.Count == 0 ? UiText.OptionsNoMods : "";

    public void OnToggleMod()
    {
        if (!ModPicked)
        {
            return;
        }

        var id = _modOrder[ModIndex].Id;
        if (!app.EnabledMods.Remove(id))
        {
            app.EnabledMods.Add(id);
        }

        ShowMods(id);
    }

    public void OnModUp() => MoveMod(-1);

    public void OnModDown() => MoveMod(1);

    private void MoveMod(int by)
    {
        if (!ModPicked || app.EnabledMods.IndexOf(_modOrder[ModIndex].Id) is var at && at < 0)
        {
            return;
        }

        var to = Math.Clamp(at + by, 0, app.EnabledMods.Count - 1);
        var id = app.EnabledMods[at];
        app.EnabledMods.RemoveAt(at);
        app.EnabledMods.Insert(to, id);
        ShowMods(id);
    }

    private void ShowMods(string? picked)
    {
        var on = app.EnabledMods.Select(id => app.AvailableMods.FirstOrDefault(m => m.Id == id)).OfType<ModListing>();
        _modOrder = [.. on, .. app.AvailableMods.Where(m => !app.EnabledMods.Contains(m.Id)).OrderBy(m => m.Name, StringComparer.OrdinalIgnoreCase)];
        var list = _mods ??= [];
        list.Clear();
        foreach (var m in _modOrder)
        {
            list.Add((app.EnabledMods.Contains(m.Id) ? UiText.ModOn : UiText.ModOff) + m.Name + " " + m.Version);
        }

        ModIndex = picked is null ? -1 : _modOrder.FindIndex(m => m.Id == picked);
        Changed(nameof(ModDetails));
    }

    public bool CheckUpdates
    {
        get => _prefs.Flag(Preferences.CheckUpdates);
        set => Keep(Preferences.CheckUpdates, value ? 1 : 0);
    }

    public bool UpdatePreReleases
    {
        get => _prefs.Flag(Preferences.UpdatePreReleases);
        set => Keep(Preferences.UpdatePreReleases, value ? 1 : 0);
    }

    /// <summary>The running version and where the last check left it.</summary>
    public string UpdateStatus =>
        string.Join(" ", new[] { app.Updates.Running.Length > 0 ? string.Format(CultureInfo.InvariantCulture, UiText.UpdateRunning, app.Updates.Running) : "", app.Updates.Status }.Where(t => t.Length > 0));

    public bool CanCheckUpdates => !app.Updates.Busy;

    public bool CanUpdate => app.Updates is { Available: not null, Busy: false };

    /// <summary>Asks GitHub now, with the pre-release choice as it stands.</summary>
    public void OnCheckUpdates()
    {
        if (CanCheckUpdates)
        {
            app.Request(new HostRequest(HostRequestKind.CheckForUpdates));
        }
    }

    /// <summary>Updates from the main menu, where the download's popup shows.</summary>
    public void OnUpdateNow()
    {
        if (CanUpdate)
        {
            app.Updates.Offered = true;
            app.Switch(ScreenMode.GameStart);
            app.StartUpdate();
        }
    }

    /// <summary>Each frame: the update line follows the host's work.</summary>
    public void Tick()
    {
        var shown = (UpdateStatus, CanCheckUpdates, CanUpdate);
        if (shown != _updateShown)
        {
            _updateShown = shown;
            Changed(nameof(UpdateStatus));
            Changed(nameof(CanCheckUpdates));
            Changed(nameof(CanUpdate));
        }
    }

    private (string, bool, bool) _updateShown;

    public void OnDone() => app.Switch(ScreenMode.GameStart);

    private string Ship(int hull) => "Combat:" + (((Fleet - 1) * 1000) + hull).ToString(CultureInfo.InvariantCulture);

    private void Keep(string name, int value, [System.Runtime.CompilerServices.CallerMemberName] string? property = null)
    {
        if (_prefs.Get(name) != value)
        {
            _prefs.Set(name, value);
            Changed(property);
        }
    }
}
