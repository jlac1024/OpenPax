namespace OpenPax.Ui;

/// <summary>Text the original keeps in its executable rather than in STR# lists.</summary>
internal static class UiText
{
    public const string DefaultPlayerName = "Commander";
    public const string ChatServer = "Server";

    // The lobby screens, which the original does not have.
    public const string MenuOptions = "Options";

    /// <summary>A colony's attack and defense as a mod rates it, in the planet list (InfoPanelPane).</summary>
    public const string ColonyRating = "+{0} ATK  +{1} DEF";

    public const string OptionsStarSpeed = "Star speed: {0}%";
    public const string OptionsFontOriginal = "The game's own";
    public const string OptionsFontUpscaled = "The game's own, upscaled";
    public const string OptionsFontModern = "Modern";
    public const string OptionsOutlineNone = "None";
    public const string OptionsOutlineColors = "Black, keeping colors";
    public const string OptionsOutlineWhite = "Black, white text";
    public const string MenuLan = "LAN (TCP/IP)";
    public const string MenuPublicLobby = "Public Lobby";
    public const string MenuPrivateLobby = "Private Lobby";
    public const string MenuPreviousMenu = "Previous Menu";
    public const string LobbyLanHeading = "LAN (TCP/IP)";
    public const string LobbyPublicHeading = "PUBLIC LOBBY";
    public const string LobbyPrivateHeading = "PRIVATE LOBBY";
    public const string LobbyLanHelp = "Lobbies on this network are listed as they answer. Pick one, give the name the others will see you by, then CONNECT.";
    public const string LobbyPublicHelp = "Give the name the others will see you by, then CONNECT.";
    public const string LobbyPrivateHelp = "Type the lobby's address or DNS name, as host, host:port or wss://host, and the name the others will see you by. Then CONNECT.";
    public const string LobbyPublicLine = "Lobby: ";
    public const string LobbyPrivateLabel = "Lobby address: host, host:port, or wss://host";
    public const string LobbyPickOne = "Pick a lobby from the list first.";
    public const string LobbyNoScan = "Cannot look for lobbies on this network: ";
    public const string LobbyFoundLine = "{0}  {1}  ({2} in it){3}";
    public const string LobbyOtherVersion = "  another version";
    public const string LobbyConnecting = "Connecting...";
    public const string LobbyCannotReach = "Could not reach the lobby: ";
    public const string LobbyBadAddress = "That is not an address.";
    public const string LobbyLost = "The connection to the lobby was lost.";
    public const string RoomsTitle = "ROOMS";
    public const string RoomHost = "Host ";
    public const string RoomPlayers = "Players";
    public const string RoomLocked = "Password";
    public const string RoomModsLabel = "Mods:";
    public const string RoomModsNone = "none";
    public const string Yes = "yes";
    public const string No = "no";
    public const string RoomLockedMark = " (password)";
    public const string RoomPlayingMark = " (playing)";
    public const string RoomNameSuffix = "'s game";
    public const string RoomHostMark = " (host)";
    public const string RoomDesignedMark = " (designed)";
    public const string RoomReadyMark = " - ready";
    public const string RoomWaiting = "Waiting for everyone to be ready.";
    public const string RoomAllReady = "Everyone is ready. START when you are.";
    public const string RoomMemberHelp = "Mark yourself READY; the host starts the game.";
    public const string RoomStarting = "Starting the game...";
    /// <summary>The system view of a star never seen.</summary>
    public const string Unexplored = "Unexplored";

    /// <summary>Ends the abandon question after the planet's name.</summary>
    public const string QuestionEnd = "?";

    /// <summary>The planet panel's work line: ships queued, then the head's class and name.</summary>
    public const string QueueSeparator = " / ";

    public const string ClassSeparator = ": ";

    // The developer console's words (the port's own; not in the game's data).
    public const string ConsoleWelcome = "OpenPax console. Type help.";
    public const string ModOn = "[on]  ";
    public const string ModOff = "[off] ";
    public const string OptionsNoMods = "No mods: put them in the mods folder beside the config file.";
    public const string PauseAsked = "Asking the other players to pause the game...";
    public const string PauseVote = "{0} asks to pause the game. Do you agree?";
    public const string PauseDeclined = "The game goes on: not everyone agreed to pause.";
    public const string ConsoleFps = "{0:0.0} fps ({1:0.0} ms a frame)";
    public const string ConsoleNoFps = "No frame time yet.";
    public const string ConsoleHelp = "help, players, hash, fps, cheats on, cheats off, devwindow, gallery, art hd|classic, font original|upscaled|modern, outline off|colors|white";
    public const string ConsoleCheatHelp = "cheats: money buildall kill research spies ships colonize explore planetcombat fleetcombat synctest [player number]";
    public const string NoticeOk = "OK";
    public const string UpdateOffer = "OpenPax {0} is out (you have {1}). Update now? The game restarts when it is done.";
    public const string UpdateCancel = "CANCEL";
    public const string UpdateStarting = "Getting OpenPax {0}...";
    public const string UpdateRunning = "You have OpenPax {0}.";

    // The room's mods (docs/modding/multiplayer.md).
    public const string ModsWaiting = "Asking the host for the room's mods...";
    public const string RoomModsHost = "The room plays your mods. Turn them on and off here; the players fetch them from you.";
    public const string RoomModsInStep = "You have the room's mods.";
    public const string RoomNoMods = "The room plays without mods.";
    public const string RoomModFetching = " (fetching)";
    public const string RoomSyncingMark = " (fetching mods)";
    public const string ModsFetching = "Fetching {0} mod(s) from the host: {1} of {2} KB";

    /// <summary>Beside the pointer with ATTACK waiting: the chance a mod gives the attack (MapPane).</summary>
    public const string AttackOdds = "{0}%";
    public const string GameSavedNotice = "The game was saved as {0}.";
    public const string ConsoleCheatsOn = "Cheats on.";
    public const string ConsoleCheatsOff = "Cheats off.";
    public const string ConsoleCheatsAreOff = "Cheats are off. Type: cheats on";
    public const string ConsoleNoGame = "No game is running.";
    public const string ConsoleNoPlayer = "No such player. Type: players";
    public const string ConsoleUnknown = "Unknown command. Type: help";
    public const string ConsoleDone = "{0} for {1}.";
    public const string ConsoleSet = "Set.";
    public const string ConsoleYou = " (you)";
    public const string ConsoleOut = " (eliminated)";

    /// <summary>Before the planet panel's population growth.</summary>
    public const string Plus = "+";

    /// <summary>The notice after a load, around the save's name.</summary>
    public const string LoadedGame = "Loaded game ";

    public const string LoadedGameEnd = " successfully.";

    /// <summary>A plan's name (plan +4): the player's own, or the stock name from STR# 103.</summary>
    public static string PlanName(UiAssets assets, Sim.Players.Player player, int slot)
    {
        return player.PlanNames[slot] ?? assets.Strings.Get(new OpenPax.Data.Views.TextRef(103, slot));
    }
}
