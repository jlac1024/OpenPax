namespace OpenPax.Ui.Screens;

/// <summary>The full screens the app switches between, numbered as the original numbers them.</summary>
public enum ScreenMode
{
    Victory = 0,
    GameStart = 1,

    /// <summary>The port's own: connecting to a lobby. The original's network screens used modes 2 to 7; these take their place.</summary>
    LobbyConnect = 2,

    /// <summary>The port's own: the lobby's rooms and chat.</summary>
    LobbyRooms = 3,

    /// <summary>The port's own: a room before its game starts.</summary>
    LobbyRoom = 4,

    /// <summary>The port's own: Game Start's Options (player name, flag, fleet, species, display).</summary>
    Options = 5,
    SpeciesSelection = 8,
    PlayerDesign = 9,
    GameSetup = 10,
    MainControl = 11,
    SectorDisplay = 12,
    Research = 13,
    PlanetarySurface = 14,
    ShipDesign = 15,
    DomesticPolicy = 16,
    FleetCommand = 17,
    ForeignAffairs = 18,
    GameOptions = 19,
    Combat = 20,
    Developer = 21,
}
