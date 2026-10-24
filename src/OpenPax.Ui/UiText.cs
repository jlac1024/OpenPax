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
    public const string FixesQuestion = "OpenPax can fix bugs to the original game, such as ships given to the computer never reaching it, trades of over 21 ships losing some, and fast shots missing planets. Turn on all the fixes?";
    public const string FixesNote = "They apply to new games; a game already started keeps its rules. Each fix can be turned on or off in Options, GAME. Network games use the host's.";
    // Tooltips: what a thing on the economy and planet screens is for (Screen.TooltipAt). A heading, then '\n' and the explanation.
    public const string TipTreasury = "Imperial Treasury\nYour money. Each year it gains your net income. If it runs out, construction subsidies are cut first, then research, espionage and construction spending, and upkeep left unpaid damages your ships and orbitals.";
    public const string TipAnnualIncome = "Annual Income\nWhat your colonies earn in a year, less the upkeep of your orbitals and shipyards. It grows with population, wealth rating and finance improvements, and with trade treaties.";
    public const string TipResearchSpending = "Research spending\nHow much of the upkeep of your research improvements you pay, 0 to 100%. Your colonies make that share of their research points, which discover new technologies.";
    public const string TipEspionageSpending = "Espionage spending\nHow much of the upkeep of your espionage improvements you pay, 0 to 100%. Your colonies make that share of their espionage points, which pay for spy missions and guard against other empires' spies.";
    public const string TipConstructionSpending = "Construction spending\nHow much of the upkeep of your construction improvements you pay, 0 to 100%. Your colonies make that share of their construction points, which build ships, orbitals and improvements.";
    public const string TipSubsidyCost = "CP Subsidy Cost\nWhat you pay in a year to buy construction points at your colonies, 20 credits for each point. Bought points build that colony's ships, orbitals and improvements sooner. Set it for a colony on its planet screen (F7). When money runs short, subsidies are cut first.";
    public const string TipFleetCost = "Fleet Cost\nThe yearly upkeep of your ships. When the treasury cannot pay it, ships are damaged and can be lost.";
    public const string TipNetIncome = "Net Income\nWhat the treasury gains, or loses when negative, in a year: income less spending, subsidies and all upkeep.";
    public const string TipFilters = "Display Filters\nWhich planets the list below shows: by how well your species can live there (Good, Fair, Bad) and by status (your Colonized planets, Explored planets with no colony).";
    public const string TipDomesticAdvisor = "Domestic advisor\nTheir skill raises every colony's construction. REPLACE picks another.";
    public const string TipEmpire = "Empire\nWho the colony belongs to. Click a caption to sort the planets by it.";
    public const string TipPlanet = "Planet\nThe planet's name. Click a column to pick its planet; double click to see its surface.";
    public const string TipHabitability = "Habitability\nHow well your species can live there: good with breathable air and a tolerable temperature, fair with one of the two, bad with neither. It sets how many people the planet holds and how fast they grow.";
    public const string TipWealth = "Wealth Rating\nThe planet's riches: the income its people make is multiplied by it.";
    public const string TipPopulation = "Population\nPeople now and the most the planet holds, and how much they grow in a year. People make the colony's income, research, espionage and construction.";
    public const string TipColonyIncome = "Income\nWhat the colony earns in a year, less the upkeep of its orbitals.";
    public const string TipColonyEspionage = "Espionage\nThe colony's espionage points in a year: they pay for spy missions and guard against other empires' spies. Measured against your empire's people, so it can read below zero.";
    public const string TipColonyResearch = "Research\nThe colony's research points in a year, toward new technologies.";
    public const string TipColonyConstruction = "Construction\nThe colony's construction points in a year. They go to its shipyard first, then to repairing and building orbitals, then to its improvements in plan order.";
    public const string TipColonySubsidy = "CP Subsidy\nBuys this colony construction points with money: each point adds one construction point a year, on top of what its people make, for 20 credits a year. Its shipyard, orbitals and improvements get built sooner. The points do not depend on its population or your Construction spending. When money runs short, subsidies are cut first.";
    public const string TipProject = "Project\nThe colony's plan: which improvements it builds first. The arrows step through Quickest, Population, Construction, Research, Espionage and Finance.";
    public const string TipSurfaceHabitability = "Wealth and habitability\nThe planet's wealth rating multiplies the income its people make. The face shows how well your species can live there.";
    public const string TipNetIncomeSurface = "Annual Income\nYour empire's net income in a year (the Domestic Policy screen's Net Income), after this colony's subsidy and all other spending.";
    public const string TipOrbitals = "Orbitals\nThe defenses and stations the colony has: fighter and missile bases, battle stations, mine fields, academies and shipyards. Build them from the Sector Display's planet panel.";
    public const string TipWorkQueue = "Construction queue\nWhat the colony builds: its shipyard's ship or a repair first, then its improvements in plan order. EXTEND LIST shows the whole list.";
    public const string TipColonyArrows = "Colonies\nStep through your colonies.";

    public const string LicensesLicense = "License: {0}";
    public const string MenuLicenses = "Licenses";

    public const string SaveModsHeading = "Mods not loaded";
    public const string SaveModsChangedHeading = "Mods changed since this save";
    public const string SaveModMissing = "{0} {1}: not turned on";
    public const string SaveModMissingUnknown = "{0} {1}: not in the mods folder";
    public const string SaveModVersion = "{0}: saved with {1}, {2} is turned on";
    public const string SaveModsBlocked = "Turn them on in Options, MODS, to load this game.";
    public const string SaveNeedsMods = "{0} cannot be loaded: it was played with mods that are not turned on ({1}).";

    public const string OptionsMasterVolume = "Master volume: {0}%";
    public const string OptionsMusicVolume = "Music: {0}%";
    public const string OptionsEffectsVolume = "Sound effects: {0}%";
    public const string OptionsVoicesVolume = "Voices: {0}%";
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
    public const string OptionsNoMods = "No mods: put them in the mods folder beside the config file.";
    public const string PauseAsked = "Asking the other players to pause the game...";
    public const string PauseVote = "{0} asks to pause the game. Do you agree?";
    public const string PauseDeclined = "The game goes on: not everyone agreed to pause.";
    public const string ConsoleFps = "{0:0.0} fps ({1:0.0} ms a frame)";
    public const string ConsoleNoFps = "No frame time yet.";
    public const string ConsoleHelp = "help, players, hash, fps, cheats on, cheats off, devwindow, gallery, art hd|classic, font original|upscaled|modern, outline off|colors|white";
    public const string ConsoleCheatHelp = "cheats: money buildall kill research spies ships colonize explore planetcombat fleetcombat synctest [player number]";
    public const string NoticeOk = "OK";
    public const string UpdateInstalledOutside = "A new version of OpenPax was installed while the game was running. Restart now to use it?";
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
