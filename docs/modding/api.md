# Mod API reference

Written by `tools/ApiDocs` from the game's assemblies and their doc comments each build; do not edit by hand. The guides are in [modding.md](../modding.md). Members marked for mods (`[ModApi]`) come first on `Game`; the rest of its public surface follows, as the game's own screens use it.

## Contents

- **Writing a mod**: [IMod](#type-imod), [ModContext](#type-modcontext), [ModStore](#type-modstore), [ModRandom](#type-modrandom), [ModInfo](#type-modinfo), [ModManifest](#type-modmanifest)
- **The game**: [Game](#type-game), [ModEvent](#type-modevent), [ModEventKind](#type-modeventkind), [ModFight](#type-modfight), [ColonyRating](#type-colonyrating), [Fixes](#type-fixes), [GameNotice](#type-gamenotice), [NoticeKind](#type-noticekind)
- **The galaxy**: [Universe](#type-universe), [Star](#type-star), [Planet](#type-planet), [Wormhole](#type-wormhole), [ISpaceObject](#type-ispaceobject)
- **Players, colonies and ships**: [Player](#type-player), [Treaty](#type-treaty), [Colony](#type-colony), [Structure](#type-structure), [Fleet](#type-fleet), [Design](#type-design), [Component](#type-component), [Species](#type-species), [IncidentKind](#type-incidentkind)
- **Game tables**: [GameTables](#type-gametables), [GameSetup](#type-gamesetup), [TechInfo](#type-techinfo), [HullInfo](#type-hullinfo)
- **Loading (the game's side)**: [ModHost](#type-modhost), [ModCatalog](#type-modcatalog)

## Writing a mod

A mod's entry point and what it registers at load.

<a id="type-imod"></a>

### IMod

`interface OpenPax.Modding.IMod`

A C# mod's entry point (docs/modding.md): a public class with a public constructor taking nothing. The game makes one when it starts and calls `IMod.Load` once, where the mod says what it changes.

#### Members

- `void Load(ModContext context)`


<a id="type-modcontext"></a>

### ModContext

`class OpenPax.Modding.ModContext`

What a C# mod may change (docs/modding.md), given to `IMod.Load`. A mod's rules run where the game is run: in single player, or on the server of a network game, whose players all hold the same mods (the lobby sees to it), so a game stays one game.

#### Members

- `string Id { get; }`: The mod's id, which prefixes its keys in a game's mod state.
- `ModInfo Mod { get; }`: The mod's manifest and folder.
- `void ChangeTables(Func<GameTables, GameTables> change)`: Changes the game tables (techs, hulls, species, names) every game is made from; mods change them in their order.
- `void Log(string line)`: A line for the player to see when the game starts, and on the console: what the mod wants known.
- `void OnAttackOdds(Func<Game, Fleet, ISpaceObject, double?> odds)`: Gives the chance, 0 to 1, that a fleet's attack on a fleet or a planet succeeds (`Game.RateAttack`), shown beside the pointer; null leaves it to the next mod.
- `void OnBattle(Func<Game, Fleet, ISpaceObject, bool> handler)`: Has a say over each battle about to begin (`Game.TakeOverBattle`): given the attacker and its target, true takes it over and no battle opens. The first mod in order to take a battle over has it.
- `void OnColonyChangedHands(Action<Game, Player, Player, Planet> handler)`: A colony went from one player (the third argument) to another (the second).
- `void OnColonyDestroyed(Action<Game, Player, Planet> handler)`: A colony was destroyed or abandoned (not one changing hands).
- `void OnColonyFounded(Action<Game, Player, Planet> handler)`: A colony was founded (not one changing hands).
- `void OnColonyRating(Func<Game, Colony, ColonyRating?> rating)`: Gives a colony's attack and defense for the planet lists to show (`Game.RateColony`); null leaves it to the next mod. The first mod in order to rate a colony has it.
- `void OnEvent(Action<Game, ModEvent> handler)`: Hears every event of a game (`Game.Happened`) as it happens, for every player.
- `void OnEvent(ModEventKind kind, Action<Game, ModEvent> handler)`: Hears the events of one kind.
- `void OnNewGame(Action<Game> action)`: Acts on a new game once it is made, before its first step (not on a loaded one).
- `void OnPlayerEliminated(Action<Game, Player> handler)`: A player was eliminated.
- `void OnShipBuilt(Action<Game, Design, Planet> handler)`: A ship was built, at the planet given.
- `void OnShipDestroyed(Action<Game, Design, Player, Player?> handler)`: A ship was destroyed; the player that did it when known.
- `void OnStep(Action<Game> action)`: Acts after every step of a game, new or loaded, before the step's state hash.
- `void OnTechLearned(Action<Game, Player, TechInfo> handler)`: A player learned a tech.
- `void OnTreatyChanged(Action<Game, Player, Player, int, int> handler)`: A treaty slot changed: from, toward, slot (Treaty.War and on), new value (Treaty.Off and on).
- `ModRandom Random(Game game)`: The mod's own random numbers in a game (ModRandom), deterministic and saved with it.
- `IReadOnlyDictionary<string, object> ReadToml(string fileName)`: A TOML file of the mod's folder, read once at load (its settings, for one): keys to values (long, double, bool, string, lists, tables). An empty table when the file is missing; a file that does not read is logged and empty.
- `ModStore Store(Game game)`: The mod's own saved values in a game (ModStore): saved, sent to clients and hashed.


<a id="type-modstore"></a>

### ModStore

`class OpenPax.Modding.ModStore`

A mod's own saved values in a game (`Game.ModState`), under keys of its own: "id.key". They are saved with the game, sent to network clients and part of the state hash, so a value set the same way in every game keeps games in step. Numbers are kept as invariant text that reads back exactly.

#### Members

- `new ModStore(Game game, string modId)`
- `IReadOnlyList<string> Keys { get; }`: The mod's keys that have a value, in ordinal order, without the mod's prefix.
- `string? Get(string key)`
- `bool GetBool(string key, bool fallback = False)`
- `double GetDouble(string key, double fallback = 0)`
- `int GetInt(string key, int fallback = 0)`
- `long GetLong(string key, long fallback = 0)`
- `void Remove(string key)`
- `void Set(string key, string? value)`: Sets a value; null removes it.
- `void SetBool(string key, bool value)`
- `void SetDouble(string key, double value)`
- `void SetInt(string key, int value)`
- `void SetLong(string key, long value)`


<a id="type-modrandom"></a>

### ModRandom

`class OpenPax.Modding.ModRandom`

Random numbers for a mod's rules that keep a game deterministic: a stream of its own (xorshift64*), seeded from the game's seed and the mod's id, its state kept in the mod's store ("id.random"), so it is saved, sent to network clients and hashed like the rest of the game. Never use System.Random or the clock in rules: games would part ways. The game's own stream (Game.Random) is left alone, so the original's rules draw as they did.

#### Members

- `new ModRandom(Game game, string modId)`
- `bool Chance(double chance)`: True with the chance given, 0 to 1.
- `int Next(int max)`: A whole number from 0 to max - 1 (0 when max is 1 or less).
- `int Next(int min, int max)`: A whole number from min to max - 1.
- `double NextDouble()`: A number from 0 up to but not including 1.


<a id="type-modinfo"></a>

### ModInfo

`record OpenPax.Modding.ModInfo`

A mod found in the mods folder: its manifest and where it is.

#### Members

- `new ModInfo(ModManifest Manifest, string Folder)`: A mod found in the mods folder: its manifest and where it is.
- `string? AssemblyPath { get; }`: Its assembly's path, or null when it brings none.
- `string? DataFolder { get; }`: Its data pack's folder, or null when it brings none.
- `string Folder { get; init; }`
- `string Hash { get; }`: SHA-256 of its files (ModPackage.HashOf), worked out when it is first asked for.
- `ModKey Key { get; }`: The mod as a mod set names it.
- `ModManifest Manifest { get; init; }`


<a id="type-modmanifest"></a>

### ModManifest

`record OpenPax.Modding.ModManifest`

A mod's mod.toml (docs/modding.md): who it is, and what it brings: a data pack (its data folder, laid out as the game's), a C# assembly, or both.

#### Members

- `new ModManifest(string Id, string Name, string Version, string Description, string? Assembly)`: A mod's mod.toml (docs/modding.md): who it is, and what it brings: a data pack (its data folder, laid out as the game's), a C# assembly, or both.
- `string? Assembly { get; init; }`
- `string Description { get; init; }`
- `string Id { get; init; }`
- `string Key { get; }`: How a game and the lobby name this mod: id and version.
- `string Name { get; init; }`
- `string Version { get; init; }`
- `static ModManifest Parse(string text)`: A mod's manifest; throws `FormatException` with what is wrong.
- `const string DataFolder = "data"`: The folder of a mod's data pack, beside its manifest.
- `const string FileName = "mod.toml"`


## The game

The running game, its events and what mods report back to the screens.

<a id="type-game"></a>

### Game

`class OpenPax.Sim.Game`

Advisors over time, and orders about them (docs/sim/advisors.md).

#### Made for mods

- `Fixes Fixes { get; set; }`: The fixes to the original's bugs this game plays with (`Fixes`); none plays as the original.
- `Dictionary<string, string> ModState { get; }`: Mods' own state, saved with the game and sent to network clients: one string per key, by each mod's own keys.
- `List<string> Mods { get; }`: The mods this game plays with, as "id@version" in their order (docs/modding.md); empty for none.
- `List<Player> Players { get; }`
- `Func<Game, Fleet, ISpaceObject, double?>? RateAttack { get; set; }`: A mod's chance, 0 to 1, that a fleet's attack on a fleet or a colony succeeds, which the map shows beside the pointer with ATTACK waiting; null shows nothing, as the original. Not saved: the mods set it again.
- `Func<Game, Colony, ColonyRating?>? RateColony { get; set; }`: A mod's rating of a colony's attack and defense, which the planet lists show when set; null shows nothing, as the original. Not saved: the mods set it again.
- `GameSetup Setup { get; }`
- `GameTables Tables { get; }`: The game data this game was started with.
- `Func<Game, Fleet, ISpaceObject, bool>? TakeOverBattle { get; set; }`: A mod's say over a battle about to begin: given the attacking fleet and its target (a fleet, or a planet with a colony), true takes the battle over, so none opens and the game does not stop for it. A fleet attacking a planet then circles it. Not saved: the mods set it again.
- `long Tick { get; }`: Ticks run since the game started.
- `Universe Universe { get; }`
- `static bool AtWar(Player a, Player b)`: Whether one player is at war with another.
- `void Bombard(Colony colony, double amount)`: Bombardment of a colony's people: its population falls by the amount, to no less than none.
- `void CaptureColony(Colony colony, Player taker)`: A colony taken by another player, as a planet traded is handed over; the two are at war after.
- `static double ComponentHitChance(Design ship)`: The chance, 0 to 1, that a hit on the ship lands on one of its components instead of its hull, as in the original's battles: higher the more damaged the ship and the poorer its build and armor.
- `static int DamageComponents(Design ship, int amount)`: Damage to a ship's components (sensors, ECM, shield, weapons, specials), spread by the space each takes, as many hits would leave it: damaged weapons fire weaker and a damaged shield stops less. Returns what the components could not take, for the hull.
- `void DamageDefenses(Colony colony, double amount)`: Damage to a colony's defense structures, the first stack first; a stack loses a unit for each unit's worth of hit points gone.
- `bool DamageShip(Design ship, int amount, Player? by = null)`: Damage to a ship's structure; at none the ship is lost from its fleet, which a credited player is told of. True when it is lost.
- `double DamageStructure(Structure stack, double amount)`: Damage to one structure stack: it loses a unit for each unit's worth of hit points gone, and is gone with the last. What it could not take is given back.
- `void DeclareWar(Player a, Player b)`: Two players at war both ways, as the original's battles make them at the first hit.
- `int DefenseAttack(Colony colony)`: A colony's defenses' attack: each defense structure's weapons' best damage, by its count.
- `static double DefenseHitPoints(Colony colony)`: What is left of a colony's defense structures.
- `IReadOnlyList<ModFight> Fights(Star star)`: The fights last reported in one system whose two sides are still there.
- `bool Fixed(Fixes fix)`: Whether this game plays with a fix.
- `static void GiveMoney(Player player, double amount)`: Credits to a player, or taken from one with a negative amount.
- `void MakePeace(Player a, Player b)`: Two players at peace both ways: Neutral in force, which ends a war, as an accepted peace does.
- `int MineFieldLevel(TechInfo field)`: A mine field's level, its place among the mine field techs from 1: a minesweeper of that level or better sees its mines.
- `int MinesweeperLevel(Design design)`: The level of the first minesweeper fitted, 0 for none; a minefield of a higher level stays unseen by the ship.
- `void ReportFights(IEnumerable<ModFight> fights)`: The fights a mod is running without the battle screen, for the system view to show (a battle marker, and the health of each side). Each report replaces the last; none clears them.
- `void SetPopulation(Colony colony, double population)`: A colony's population set, no lower than none; its owner hears of the change as of growth.
- `int ShipAttack(Design ship)`: A ship's attack: its damage per hit when a battle is resolved at once.
- `static int ShipDefense(Design ship)`: A ship's defense rating in battle.
- `Fleet SpawnShips(Player player, Planet planet, Design design, int count)`: A new fleet of the player's at a planet, with ships built to a design (one of the player's own, or any other). A human's fleet circles the planet, a computer player's sits on it, as when a shipyard makes one. At most 21 ships fit in a fleet; the rest are not made.
- `void TeachTech(Player player, TechInfo tech)`: A tech learned as if from outside research (espionage): known, taken up by the player's colonies, designs and best armor, and a research field working on it freed. Nothing when the player knows it already.
- `void Tell(Player player, string text)`: Tells a human player something, in the status line and the history like the game's own notices (a computer player is told nothing). In a network game it reaches the player's own machine.
- `static int TreatyLevel(Player from, Player toward)`: The highest treaty in force from one player toward another (Treaty.War to Treaty.ShareInformation), or Treaty.None when they have not met.
- `event Action<Game, ModEvent>? Happened`: Everything a `ModEvent` tells of, as it happens. Not saved: the mods subscribe again.
- `event Action<Game>? Stepped`: After each step, before its hash is taken: where a mod's rules act (docs/modding.md). Not saved; whoever runs the game (single player, or the server) attaches the mods' handlers again on loading.

#### Also public

- `List<Battle> Battles { get; }`: Battles waiting, newest first (game +0x9C98). While any exist, strategic time stands still.
- `Calendar Calendar { get; }`
- `int ComputerPlayerCursor { get; }`: Where the computer player task looks for the next player to think (task +0x10).
- `IComputerPlayer? ComputerPlayers { get; }`: The computer player modules, run by the AI task.
- `bool FightStepByStep { get; set; }`: Battles with a human side are fought step by step, as in the original; off, every battle is resolved at once (some tests use that).
- `bool InCombat { get; }`
- `int LastThinker { get; }`: The player the computer player task picked last, or -1. Kept for tests and the replay log.
- `int LeaveCounter { get; }`: How many times leaving ships have gone on toward their exit (starting at 1, shared by every battle): past 100 the ship then moving picks its way out again.
- `Player? LocalPlayer { get; }`: The player at this machine: the first human, or null in a game of computer players only.
- `int NextBattleId { get; }`: The next battle's number.
- `int NextCombatObjectId { get; }`: The next battle object's id (never reset in the original); 0 is never used.
- `int NextMissionId { get; }`: The next espionage mission's number (kept in saved games).
- `ObjectIds ObjectIds { get; }`: Ids of objects in the universe: stars, planets, wormholes, fleets.
- `bool Over { get; }`: True once the victory check has ended the game (+0x9C96).
- `GameRandom Random { get; }`: The one game stream (docs/numerics.md).
- `GameRecording? Recording { get; }`: The recording for a replay, once started; not saved or sent with the game.
- `Scheduler Scheduler { get; }`: Single player for now; networked games drop the autosave task.
- `ObjectIds SecondaryIds { get; }`: Ids of advisors, designs and ships.
- `int SyncTests { get; }`: How many Special Tests were asked for (the original's sync dump counter).
- `StepEvents Abandon(Planet planet)`: Op 1, abandon: a nomad species first leaves in colony ships, one per ten population plus one, in a new fleet; then the colony is destroyed, which can eliminate the player and end the game.
- `SpyMission AddMission(Player owner, Player target, int kind, int level)`: Starts a mission at the back of the owner's list.
- `static double AnnualIncome(Player p)`: Gross income less structure upkeep (Annual Income).
- `StepEvents AnswerPackage(TradePackage package, bool accept)`: Answers a package. Accepting checks both halves and, if they hold, moves everything with an incident for each part; rejecting is an incident. Either way the first matching pending package goes.
- `bool AnythingToResearch(Player p)`: Whether anything at all can be researched now.
- `StepEvents Apply(Command command)`: Carries out a command between ticks. One naming a player, object or slot that is gone or not the player's does nothing.
- `void ApplyBattleOrder(BattleOrder order)`: Runs an order. The selection is read back as the original reads its message: each ship still in the current battle goes to the front, so the ships are taken in the reverse of the order given. An order to an orbital goes to it alone.
- `StepEvents ApplyCheat(Player player, Cheat cheat)`: Applies a cheat for a player. Special Test only counts (the original wrote a sync dump file).
- `void AppointGovernor(Player p, int advisorId, int role)`: A governor becomes the advisor for a field (0 to 3), replacing the one there; its star gets a new governor. No cost.
- `static bool AtShipyard(Fleet fleet)`: The fleet circles a planet whose colony, of any owner, has a shipyard.
- `Design AutoDesign(Player player, int hull, string name)`: AUTO: a blank design of the hull with the best armor, filled by the builder as computer players' designs are.
- `static bool BeingResearched(Player player, int tech)`: A field of the player is researching the tech.
- `int BestOfKind(Player p, int kind)`: The highest-id known tech of a kind, or -1.
- `int BestOrbital(Player player, int kind)`: The best known tech of a structure kind, or -1.
- `bool CanBuildOrbital(Colony colony, int kind)`: The build button for a structure kind would be offered (the six predicates).
- `static bool CanColonizeFrom(Star star, Player player)`: A star holds a fleet of the player with a colony ship, orbiting or not.
- `bool CanReach(Fleet fleet, int toStar)`: Whether the fleet's fuel reaches a star by the shortest route.
- `void CancelMission(Player owner, int missionId)`: Drops a mission and what was put into it.
- `static void ChangeFunding(Player player, Planet planet, int value)`: Command 10, action 1050: a colony of the player gets the subsidy asked for, as a change from what it has. The player's total takes the same change, neither going below 0.
- `StepEvents ColonizeNow(Player player, Planet planet, Fleet fleet)`: The "c" key: a colony at once on a planet with none. Quirk kept: it does not check that the fleet still has a colony ship (the colony is then founded as generation 1) or still orbits the planet.
- `static int ColonyShips(Fleet fleet)`: Ships in the fleet carrying a colonizer.
- `int ColonyStrength(Colony colony)`: The sum of a colony's structure strengths.
- `static double ColonyUpkeep(Colony c)`: A colony's improvement upkeep at its owner's spending percents, plus its structures'.
- `static void CopyPlan(Player player, int from, int to, string name)`: Op 7: a copy of a plan in an empty slot, named as given ("Unnamed" in the game).
- `static Structure? Damaged(Colony colony)`: The colony's first structure not fully built or repaired, or null.
- `static void DeleteDesign(Player player, int slot)`: Op 1: the slot is emptied. Ships already built keep their own copy.
- `static void DeletePlan(Player player, int slot)`: Op 6: the plan goes and its colonies follow plan 0.
- `Design? FindShip(int shipId)`: A ship by its id, looked for system by system, fleet by fleet.
- `static int[] FinishedByResource(Colony colony)`: Finished improvements by the resource each works on, 0 credits to 4 population. Quirk kept: only exactly 100 percent counts.
- `void FireGovernor(Player p, int advisorId)`: Fires a star's governor; the star gets a new one.
- `static double FixedUpkeep(Player p)`: Structure and ship upkeep a year.
- `static int FleetCondition(Fleet fleet)`: A fleet's ships' average condition, in whole percent.
- `void FleetOrder(Fleet fleet, FleetOrderKind kind, Order? order, bool replace)`: A fleet order: with replace, the orders so far are removed one by one (so a fleet set to colonize may found its colony where it is); then move to the target, toggle intercepting, or go and colonize or attack.
- `bool FleetRoute(Fleet fleet, int toStar, FleetOrderKind kind, bool replace, IReadOnlyCollection<int>? splitOff = null)`: A fleet order to another star: refused when the fleet cannot reach it; otherwise an order of the kind for each wormhole on the way, replacing the old orders on the first only. Quirk kept: an attack or colonize kind is given for every hop, so the fleet travels in that mode.
- `void GiveOrder(Fleet fleet, Order order)`: Gives a fleet an order. The first order takes the fleet out of its orbit; a later one goes on the end unless it leads to the same place as the last.
- `ulong Hash()`
- `static bool InTraining(Fleet fleet)`: The fleet is training at its owner's academy.
- `bool Knows(Player p, int kind)`: Whether the player knows any tech of a kind.
- `void MarkRefit(Player player, int shipId, int designSlot)`: Action 1, mark for refit: the ship gets a new ship of the design in the slot, built when the yard where its fleet circles has nothing to do (`Game.StartRefit`). Quirk kept: the yard's owner is not checked here, so a mark at a foreign yard never starts. An older mark is replaced.
- `bool MinesVisible(Battle battle, int owner, int viewer)`: Whether a side sees a player's mines: its own, a player it is not at war with, or with a ship whose minesweeper is of the mines' level or better. Quirk kept: the newest mine's level stands for the whole list.
- `static void MovePlanEntry(Player player, int slot, int from, int to)`: Op 5: an entry moves to stand before another, or to the end when "to" is past the last.
- `static double NetIncome(Player p)`: What is left of a year's income after improvements, subsidies and upkeep. Note the products run spending times upkeep, then a hundredth, unlike the single lines.
- `Improvement? NextImprovement(Colony colony)`
- `int NextImprovementId(Colony colony)`: The next improvement the colony's plan will build, or -1.
- `int ObjectIdOf(ISpaceObject? target)`: The object id a fleet order names its target by (`Game.SpaceObject`); -1 for none.
- `void Observe(Player player)`: "Observe Game" after the end (the command, kind 7): every system is shown to the player, who goes on watching.
- `StepEvents OfferPackage(TradePackage package)`: Sends a package: a copy waits at the front of the target's list. Nothing is checked yet.
- `StepEvents OfferTreaty(Player from, Player to, int state)`: Offers a treaty state: pending on both sides, answered later by the other player.
- `void OrderOrbital(Planet planet, int tech)`: Op 0, a structure: a new unit or stack of the tech, if the kind limits allow it ( kind 2).
- `void OrderShip(Planet planet, int designSlot)`: Orders a ship of a design slot at the colony's first shipyard, at the front of its queue (mode 1).
- `void OrderShips(Player player, Planet planet, int designSlot, int count)`: Op 0, ships: each of the count goes to the front of the colony's first shipyard's queue, whatever its health ( kind 1).
- `void OrderStructure(Planet planet, int tech)`: Orders a structure at a planet's colony (mode 2).
- `static IEnumerable<Design> RefitChoices(Player player, Design ship)`: Designs a ship may be refitted to: the player's slots in order, the same hull size, and a newer design than the ship's (by design id; the field compared, +0x48, is presumed the id).
- `static void RenamePlan(Player player, int slot, string name)`: Op 8: the plan's new name.
- `StepEvents ReplyTreaty(Player me, Player other, int state, int value)`: Sets a treaty state on both sides at once: accepting or turning down an offer, declaring war, canceling, withdrawing. The other side is written first.
- `static int ResearchCostFor(Player player, TechInfo tech)`: The tech's research cost for this player, scaled by the species' level for its field.
- `void ResearchTech(Player player, int tech, int field)`: Puts a tech in a field's slot, resuming any progress saved for it (op research).
- `static bool Researchable(Player player, TechInfo tech)`: Whether the player could research the tech now: unknown, its prerequisites known, no academy for warriors.
- `IReadOnlyList<(int, int, bool)> RouteHops(Fleet fleet, int toStar, Player viewer)`: The hops of the route a fleet would take, each with whether its fuel holds there, for the map's preview. Stops at the first hop that starts at a star the owner has never seen.
- `int SaveDesign(Player player, Design design)`: Op 0: a copy of the design goes in the first free slot with the next class number and a new design id. Quirk kept: with all 100 slots taken nothing happens. Returns the slot, or -1.
- `void Scrap(Planet planet, int kind)`: Op 2, scrap: one unit of the first stack of a kind goes, with its upkeep. Nothing is refunded.
- `void ScrapShip(Player player, int shipId)`: Action 0, scrap: the ship leaves its fleet with its upkeep and is gone. Nothing is refunded. An emptied fleet stays until the star step clears it.
- `static void SetAllocation(Player player, int field, int value)`: Sets a field's share of research, never past what the other four leave of 100 (op allocate).
- `static void SetColonyPlan(Planet planet, int plan)`: Colony command op 4: the colony follows another plan. Refused while the planet is in battle; the owner is not checked.
- `static void SetFunding(Colony colony, double value)`: Sets a colony's construction funding, keeping the player's total in step.
- `void SetMode(Fleet fleet, FleetMode mode)`: What the fleet does at its last target.
- `static void SetSpending(Player p, int field, double value)`: Sets a spending percent (0 research, 1 espionage, 2 construction), whole and within 0 to 100.
- `static bool ShipInBuild(Colony colony)`: The colony's first shipyard has a ship in its queue.
- `ISpaceObject? SpaceObject(int id)`: What a fleet order can name, by object id: a star (its center), a planet, a wormhole or a fleet.
- `static double SpendingCost(Player p, int field)`: What research (0), espionage (1) or construction (2) improvements cost a year at the spending percent.
- `Fleet? SplitFleet(Fleet fleet, IReadOnlyCollection<int> shipIds)`: The split command: the ships named leave the fleet for a new one beside it, which takes the fleet's place, mode and intercept setting; the fleet keeps the rest and its orders. No draws.
- `Design StartDesign(Player player, Design? from, int hull = 0)`: A design to edit: a copy of an existing one, or a blank scout of armor class NONE; either way the player's best armor is fitted, replacing any other.
- `GameRecording StartRecording()`: Records commands and every tick's hash from now on (for a game from its first tick).
- `StepEvents Step()`: Runs one tick of the strategic game.
- `static int StepPlan(Colony colony, int step)`: The next (step 1) or previous (step -1) plan from the colony's, among the ten slots, skipping those with no plan.
- `static int StepPlan(Player player, int slot, int step)`: The next or previous slot holding a plan, wrapping; the stock plans always exist.
- `static void StopResearch(Player player, int field)`: Empties a field's slot, keeping its progress for later (op stop).
- `void StopWork(Planet planet)`: Op 3, stop: the oldest ship order goes from the first shipyard's queue (a refit's old ship returns to a fleet at the planet); with none, one unit of the first structure below full health goes. Quirk kept: that also scraps a unit only damaged in battle. Work spent is lost.
- `int StructureStrength(Structure s)`: A defensive rating: hit points, shield, weapons and fighters, scaled by condition; 0 for shipyards and training bases.
- `static double SubsidyCost(Player p)`: Construction subsidies a year: 20 credits a funded point.
- `bool Supplies(Star star, Player player)`: A system supplies a player who has a colony in it, or whose state 3 treaty partner has one.
- `StepEvents TakeEvents(Player player)`: The player's events since the last call.
- `IReadOnlyList<GameNotice> TakeNotices(Player recipient)`: The player's notices since the last call, oldest first.
- `StepEvents TreatyClick(Player me, Player other, int state)`: A treaty box clicked ( for War, Neutral and Allied for the rest). War is declared at once. Neutral is offered from war, taken at once out of an alliance (ending it), or withdrawn when offered. Allied is offered or withdrawn. The others are offered when off, and canceled or withdrawn otherwise; an offer to me is answered elsewhere.
- `static bool TreatyOpen(Player me, Player other, int state)`: Whether a treaty state's box can be clicked: War and Neutral unless in force; Trade Routes and Repair under Neutral; Allied under Neutral with both of those, unless already allied; Joint Research and Share Information under an alliance.
- `IReadOnlyList<Improvement> UnfinishedImprovements(Colony colony)`: The colony's improvements not yet finished, in its plan's order; the first is what construction works on next.
- `static double YardWork(Structure yard, double points)`: ((count - 1) * 0.5 + 1) * condition, times the first shipyard advisor's skill + 1.


<a id="type-modevent"></a>

### ModEvent

`record OpenPax.Sim.ModEvent`

Something that happened in the game, for mods (`Game.Happened`, ModContext.OnEvent). Raised for every player, computer players too, at the moment it happens inside a step, so a handler sees the game mid step.

| Parameter | Meaning |
|---|---|
| `Player` | Whose it is: who learned, built, lost the ship, founded, received, offered the treaty or was eliminated. |
| `Other` | The other player, when there is one. |
| `Place` | The planet, when there is one. |
| `State` | For a treaty: the slot (Treaty.War to Treaty.ShareInformation, or Treaty.None for all). |
| `Value` | For a treaty: the slot's new value (Treaty.Off, InForce, OfferedByMe, OfferedToMe). |

#### Members

- `new ModEvent(ModEventKind Kind, Player Player, Player? Other = null, ISpaceObject? Place = null, Design? Ship = null, TechInfo? Tech = null, int State = 0, int Value = 0)`: Something that happened in the game, for mods (`Game.Happened`, ModContext.OnEvent). Raised for every player, computer players too, at the moment it happens inside a step, so a handler sees the game mid step.
  - `Player`: Whose it is: who learned, built, lost the ship, founded, received, offered the treaty or was eliminated.
  - `Other`: The other player, when there is one.
  - `Place`: The planet, when there is one.
  - `State`: For a treaty: the slot (Treaty.War to Treaty.ShareInformation, or Treaty.None for all).
  - `Value`: For a treaty: the slot's new value (Treaty.Off, InForce, OfferedByMe, OfferedToMe).
- `ModEventKind Kind { get; init; }`
- `Player? Other { get; init; }`: The other player, when there is one.
- `ISpaceObject? Place { get; init; }`: The planet, when there is one.
- `Player Player { get; init; }`: Whose it is: who learned, built, lost the ship, founded, received, offered the treaty or was eliminated.
- `Design? Ship { get; init; }`
- `int State { get; init; }`: For a treaty: the slot (Treaty.War to Treaty.ShareInformation, or Treaty.None for all).
- `TechInfo? Tech { get; init; }`
- `int Value { get; init; }`: For a treaty: the slot's new value (Treaty.Off, InForce, OfferedByMe, OfferedToMe).


<a id="type-modeventkind"></a>

### ModEventKind

`enum OpenPax.Sim.ModEventKind`

What happened, in a `ModEvent`.

| Value | | Meaning |
|---|---|---|
| `TechLearned` | 0 | A player learned a tech (research, a breakthrough, a trade, espionage or a cheat): `ModEvent.Tech`. |
| `ShipBuilt` | 1 | A ship was finished at a shipyard, or made when nomads leave a colony: `ModEvent.Ship` at `ModEvent.Place`. |
| `ShipDestroyed` | 2 | A ship was destroyed in a battle or by a mod: `ModEvent.Ship`, by `ModEvent.Other` when known. |
| `ColonyFounded` | 3 | A player founded a colony on `ModEvent.Place`. |
| `ColonyChangedHands` | 4 | A colony on `ModEvent.Place` went to `ModEvent.Player` from `ModEvent.Other` (a trade, a capture). |
| `ColonyDestroyed` | 5 | A colony on `ModEvent.Place` was destroyed or abandoned. |
| `TreatyChanged` | 6 | A treaty slot from `ModEvent.Player` toward `ModEvent.Other` changed: `ModEvent.State` to `ModEvent.Value` (Treaty's constants). |
| `PlayerEliminated` | 7 | A player was eliminated. |

<a id="type-modfight"></a>

### ModFight

`record OpenPax.Sim.ModFight`

A fight a mod runs outside the battle screen (Game.ReportFights): a fleet in it and what the fight is over, the fleet attacked or the planet besieged. A planet's defenders are reported against it too.

#### Members

- `new ModFight(Fleet Fleet, ISpaceObject Target)`: A fight a mod runs outside the battle screen (Game.ReportFights): a fleet in it and what the fight is over, the fleet attacked or the planet besieged. A planet's defenders are reported against it too.
- `Fleet Fleet { get; init; }`
- `ISpaceObject Target { get; init; }`


<a id="type-colonyrating"></a>

### ColonyRating

`record OpenPax.Sim.ColonyRating`

A colony's fighting strength as a mod rates it (Game.RateColony): what its defenses deal a round, and what they can take.

#### Members

- `new ColonyRating(int Attack, int Defense)`: A colony's fighting strength as a mod rates it (Game.RateColony): what its defenses deal a round, and what they can take.
- `int Attack { get; init; }`
- `int Defense { get; init; }`


<a id="type-fixes"></a>

### Fixes

`enum OpenPax.Sim.Fixes`

Fixes to the original's bugs a game may play with (docs/sim/fix-candidates.md), each Jeff's pick, all off by default: with none, the game plays as the original. A game carries its own set, chosen when it starts; it is saved with the game and sent to network clients with its state.

| Value | | Meaning |
|---|---|---|
| `None` | 0 |  |
| `ComputerKeepsShips` | 1 | Ships given to or stolen by a computer player reach it, not vanish. |
| `ScrapUnderWork` | 2 | Stop Work scraps only a structure still being built, not one damaged in battle. |
| `ShipyardOverflow` | 4 | Work left over when a ship finishes reaches the next one as construction. |
| `FleetsJoinBattles` | 8 | A fleet near a fleet battle joins at its own free slot, rather than being left out. |
| `PlanetBattleSides` | 16 | Fleets near a planet battle join by their terms with each side, the side they are on terms with. |
| `ShotsHitPlanets` | 32 | A shot that would step over a planet's block hits it. |
| `ShotsLeadTrue` | 64 | Shots aim where they meet a moving target, not two steps past. |
| `ExperienceOnce` | 128 | A battle's experience is given once, not twice. |
| `OwnYardRefits` | 256 | A refit can be ordered only at the player's own shipyard, where it starts, not accepted at a foreign one and never begun. |
| `ColonizeNeedsShip` | 512 | The colonize key needs a colony ship in a fleet at the planet. |
| `TradeKeepsAllShips` | 1024 | A trade of more ships than a fleet holds fills a new fleet for the rest, rather than losing them. |
| `StrangersIndifferent` | 2048 | An empire that never met the victim of an attack does not hold it against the attacker. |

<a id="type-gamenotice"></a>

### GameNotice

`record OpenPax.Sim.GameNotice`

Something the game tells a human player: the original calls a player virtual during the tick and the screens make the text. Computer players get none. Not part of the game's state: the screens take them away.

| Parameter | Meaning |
|---|---|
| `Value` | A tech id, or an espionage mission's kind. |
| `Other` | The other player involved. |
| `Place` | The planet or yard planet involved. |
| `Count` | How many things a mission hit; for a failed mission 0 found nothing, 1 was seen, 2 failed unseen. |
| `Named` | For espionage damage: whether the spy's owner is named. |
| `Text` | For a mod's message: its words. |

#### Members

- `new GameNotice(NoticeKind Kind, Player Recipient, int Value = 0, Player? Other = null, ISpaceObject? Place = null, Design? Ship = null, int Count = 0, bool Named = False, string? Text = null)`: Something the game tells a human player: the original calls a player virtual during the tick and the screens make the text. Computer players get none. Not part of the game's state: the screens take them away.
  - `Value`: A tech id, or an espionage mission's kind.
  - `Other`: The other player involved.
  - `Place`: The planet or yard planet involved.
  - `Count`: How many things a mission hit; for a failed mission 0 found nothing, 1 was seen, 2 failed unseen.
  - `Named`: For espionage damage: whether the spy's owner is named.
  - `Text`: For a mod's message: its words.
- `int Count { get; init; }`: How many things a mission hit; for a failed mission 0 found nothing, 1 was seen, 2 failed unseen.
- `NoticeKind Kind { get; init; }`
- `bool Named { get; init; }`: For espionage damage: whether the spy's owner is named.
- `Player? Other { get; init; }`: The other player involved.
- `ISpaceObject? Place { get; init; }`: The planet or yard planet involved.
- `Player Recipient { get; init; }`
- `Design? Ship { get; init; }`
- `string? Text { get; init; }`: For a mod's message: its words.
- `int Value { get; init; }`: A tech id, or an espionage mission's kind.


<a id="type-noticekind"></a>

### NoticeKind

`enum OpenPax.Sim.NoticeKind`

What a notice to a human player is about; each matches a player notice the original posts (docs/ui/messages.md).

| Value | | Meaning |
|---|---|---|
| `ResearchCompleted` | 0 |  |
| `Breakthrough` | 1 |  |
| `ShipFinished` | 2 |  |
| `ColonyLost` | 3 |  |
| `WarChanged` | 4 |  |
| `FirstContact` | 5 |  |
| `PlayerEliminated` | 6 |  |
| `SpySucceeded` | 7 |  |
| `SpyFailed` | 8 |  |
| `SpyStopped` | 9 |  |
| `SpyDamage` | 10 |  |
| `TradeOffered` | 11 |  |
| `TradeFailed` | 12 |  |
| `TradeAccepted` | 13 |  |
| `TradeRejected` | 14 |  |
| `ModMessage` | 15 | The port's own: a message from a mod (Game.Tell), its words in the notice's Text. |

## The galaxy

Stars, planets and wormholes.

<a id="type-universe"></a>

### Universe

`class OpenPax.Sim.Galaxy.Universe`

The galaxy: stars with their planets and wormholes.

#### Members

- `new Universe(IReadOnlyList<Star> stars)`
- `IReadOnlyList<Star> Stars { get; }`
- `void AddTo(StateHash hash)`: Adds everything that defines the galaxy to a state hash.
- `int Hops(int from, int to)`: Wormhole jumps between two stars.
- `List<int> Route(int from, int to)`: The stars from one to another, both included. The wormholes form a tree, so this is the only path ( caches it per pair).


<a id="type-star"></a>

### Star

`class OpenPax.Sim.Galaxy.Star`

A star system (the original's sector, 0x100 bytes).

#### Members

- `new Star(int index, int objectId, int x, int y, int type, string name)`
- `StarCenter Center { get; }`: The star's own object at the center of its system, a fleet target.
- `List<Fleet> Fleets { get; }`: Fleets in the system, newest first (+0xF8).
- `int Index { get; }`
- `string Name { get; }`
- `int ObjectId { get; }`: Id of the star's own object (the original numbers stars first).
- `List<Planet> Planets { get; }`: Planets from the innermost orbit out.
- `int[] Sight { get; }`: What each player sees of the system (+0xB8), by player index; see docs/sim/visibility.md.
- `int Type { get; }`: Star type 0 to 6.
- `List<Wormhole> Wormholes { get; }`
- `int X { get; }`: Position on the galaxy map, 20 to 430.
- `int Y { get; }`
- `const int InView = 2`: Sight level: in view now.
- `const int Remembered = 1`: Sight level: seen before, not in view now.
- `const int Unseen = 0`: Sight level: never seen.


<a id="type-planet"></a>

### Planet

`class OpenPax.Sim.Galaxy.Planet`

A planet as the original creates it (object 0x220 bytes, init).

#### Members

- `new Planet(int objectId, int animationFrame, IReadOnlyList<Satellite> satellites)`
- `int AnimationFrame { get; }`: Starting frame of the planet's spin animation (+0x44).
- `int Breathing { get; }`: The port's own: who breathes it (BreathingLike, the original's rule by Class, for every original planet).
- `int Class { get; }`: +0x1F8: atmosphere 0 to 4, or 5 for type 4. A mod's planet type has the class it looks like.
- `Colony? Colony { get; }`: The colony on the planet, if any (+0x210).
- `bool InCombat { get; }`: Held in a battle (+0x74).
- `int Kind { get; }`: The port's own: a mod's planet type, its place in GameTables.PlanetKinds plus one; 0 for the original's.
- `int Moons { get; }`: +0x1E8: 0 to type - 1.
- `string Name { get; }`
- `int ObjectId { get; }`
- `Orbit Orbit { get; }`
- `Player? Owner { get; }`: Who owns the planet's colony.
- `int Rating { get; }`: +0x204: 1 to 5; meaning not yet known.
- `IReadOnlyList<Satellite> Satellites { get; }`
- `double Score { get; }`: +0x208: rating plus a draw per moon, kept as a double: the Wealth Rating the Domestic Policy window shows.
- `int Size { get; }`: +0x1FC: 53 for type 4, else 40. Probably the drawn size.
- `int Slot { get; }`: Orbit slot 0 (innermost) to 5.
- `int Temperature { get; }`: +0x1F4: (star type - slot + 3) * 25 at creation.
- `int Type { get; }`: +0x200: 1 to 3, or 4 (gas giant look).
- `int X { get; }`
- `int Y { get; }`
- `const int BreathingAll = 1`
- `const int BreathingAnaerobic = 3`
- `const int BreathingLike = 0`: The port's own: who breathes the planet when its type says otherwise than its class (docs/modding/content.md).
- `const int BreathingNone = 2`


<a id="type-wormhole"></a>

### Wormhole

`class OpenPax.Sim.Galaxy.Wormhole`

A wormhole, one end of a link between two stars.

#### Members

- `new Wormhole(int objectId, int animationFrame, int star)`
- `int AnimationFrame { get; }`: Starting animation frame, drawn from the game stream (+0x44).
- `bool Known { get; }`: Set once a fleet has jumped through either end (+0x8C).
- `string Name { get; }`
- `int ObjectId { get; }`
- `Wormhole? Partner { get; }`: The other end, or null if never linked.
- `int Star { get; }`: Index of the star this end sits at.
- `int X { get; }`: Position in the star system view.
- `int Y { get; }`


<a id="type-ispaceobject"></a>

### ISpaceObject

`interface OpenPax.Sim.Galaxy.ISpaceObject`

Anything with a position in a star system view: planets, wormholes, fleets.

#### Members

- `int X { get; }`
- `int Y { get; }`


## Players, colonies and ships

Empires and what they own.

<a id="type-player"></a>

### Player

`class OpenPax.Sim.Players.Player`

A player, human or computer (base constructor).

#### Members

- `new Player(int index, bool isAi, string name, int flag, int fleetColor, Species species)`: A player, human or computer (base constructor).
- `List<Advisor> Advisors { get; }`
- `bool AutoResearch { get; }`: Pick the next research of a field on its own when one completes (+0x44).
- `int BestArmor { get; }`: Highest-id armor the player knows, or 0.
- `double Caution { get; }`: How much stronger than a target a computer player's fleet must be to attack it (+0x560). Starts at 1.1 and rises 0.2 with each retreat and each fleet lost in combat.
- `List<Colony> Colonies { get; }`
- `int[] DesignCounters { get; }`: Numbers designs in their class names, per design type (+0x770).
- `Design?[] Designs { get; }`: Design slots; null where empty.
- `Design? Draft { get; }`: The design AUTO last built for the Ship Design window to take up; not part of the state hash.
- `int DraftNumber { get; }`: How many drafts AUTO has made, so a network client's screen knows when the one it asked for has come; not part of the state hash.
- `bool Eliminated { get; }`: Out of the game (+0x4F0), set by elimination: computer players with it set are skipped by the AI task.
- `double EspionagePoints { get; }`: Espionage of the period (+0x720): per head after the economy pass.
- `int Flag { get; }`
- `int FleetColor { get; }`
- `List<Fleet> Fleets { get; }`
- `double Funding { get; }`: Sum of the colonies' construction funding (+0x7F8).
- `EmpireHistory History { get; }`: Samples of ten statistics for the comparison graphs.
- `int HomePlanetSlot { get; }`
- `int HomeStar { get; }`
- `double[] ImprovementUpkeep { get; }`: Upkeep of completed improvements by resource (+0x4C0).
- `double Income { get; }`: Credits income of the last period (+0x4E8).
- `int Index { get; }`
- `bool IsAi { get; }`
- `int KnownCount { get; }`: Techs learned so far, counting a tech learned twice twice (+0x40).
- `SortedSet<int> KnownTechs { get; }`
- `List<SpyMission> Missions { get; }`: Espionage missions this player runs, paid for in order (the EspionageMan list, +0x70C).
- `double Money { get; }`
- `string Name { get; }`
- `List<TradePackage> Packages { get; }`: Trade packages offered to this player and not yet answered, newest first (+0x800).
- `double[] Patience { get; }`: A computer player's patience with demands, by proposer: 30 at the start, 10 less for every demand, never raised (its treaty module, +8).
- `string?[] PlanNames { get; }`: The names of the player's own plans (plan +4); null for the stock ones, which the screens name from STR# 103.
- `int[]?[] Plans { get; }`: The plan library (+0x700): ten slots of improvement tech ids in build order. Slots 0 to 5 hold the stock plans; 6 to 9 the player's own, null when empty.
- `double[] RelationScores { get; }`: A second per-player value from relations plus a draw (+0x210; meaning not yet known).
- `int[] Relations { get; }`: Attitude to each player slot, -3 to 3.
- `int[] Research { get; }`: Research allocation per field (weapons, shielding, ship, colonial, space), in percent.
- `double ResearchPoints { get; }`: Research points of the period (+0x718).
- `double[] ResearchRamp { get; }`: Per other player: research treaty ramp (+0x390) and research received (+0x410).
- `double[] ResearchReceipts { get; }`
- `ResearchSlot[] ResearchSlots { get; }`: One slot per research field.
- `int ResearchTotal { get; }`: Sum of the raw research costs of every tech given to the player (tech state +0x48); its level scales component sizes and costs.
- `Dictionary<int, int> SavedProgress { get; }`: Progress kept for techs whose research was switched away by hand (tech state +0x14), by tech id.
- `Species Species { get; }`
- `double[] Spending { get; }`: Spending percents for research, espionage and construction (+0x4A8), 0 to 100.
- `double[] StructureUpkeep { get; }`: Upkeep of shipyards (+0x798), of kind 6 structures (+0x7A0) and of every other structure (+0x7A8).
- `double Support { get; }`: Percent of upkeep that could be paid (+0x7B0); below 100 things decay.
- `double[] TradeRamp { get; }`: Per other player: trade treaty ramp 0 to 1 (+0x290) and credits received (+0x310).
- `double[] TradeReceipts { get; }`
- `Treaty[] Treaties { get; }`: Treaty states toward each player slot.
- `double Upkeep { get; }`: Upkeep of all ships (+0x790): their costs, times 0.8 for nomads.
- `void AddTo(StateHash hash)`
- `bool Knows(int tech)`: Known, as the original asks it: tech 0 always, -1 never.
- `const int DesignSlots = 100`
- `const int MaxPlayers = 16`
- `const int PlanSlots = 10`
- `const double StartingMoney = 5000`


<a id="type-treaty"></a>

### Treaty

`class OpenPax.Sim.Players.Treaty`

The seven treaty states one player holds toward another (player +0x10 + other * 0x20): 0 War, 1 Neutral, 2 Trade Routes, 3 Repair and Resupply, 4 Allied, 5 Joint Research, 6 Share Information (docs/sim/diplomacy.md). Each slot is `Treaty.Off`, `Treaty.InForce`, or pending: offered by this player, or offered to it.

#### Members

- `new Treaty()`
- `void AddTo(StateHash hash)`
- `bool Has(int state)`: Whether a state is in force; for `Treaty.None`, whether every slot is off (a pending offer is contact).
- `int Level()`: The highest state in force, or `Treaty.None`.
- `int Slot(int state)`: A slot's raw value: off, in force or pending.
- `const int Allied = 4`
- `const int InForce = 1`
- `const int JointResearch = 5`
- `const int Neutral = 1`
- `const int None = 200`: "No state set": the level of a pair with no contact.
- `const int Off = 0`
- `const int OfferedByMe = 2`
- `const int OfferedToMe = 3`
- `const int Resupply = 3`
- `const int ShareInformation = 6`
- `const int States = 7`
- `const int TradeRoutes = 2`
- `const int War = 0`


<a id="type-colony"></a>

### Colony

`class OpenPax.Sim.Players.Colony`

A colony on a planet; see docs/sim/economy.md.

#### Members

- `double ConstructionPoints { get; }`: Construction points to spend this period (+0x158).
- `double CurrentPopulation { get; }`
- `bool Destroyed { get; }`: Set once the colony is gone; loops that started before stop working on it.
- `double[] Flat { get; }`: Flat amount of each resource (+0x60).
- `double Funding { get; }`: Credits put into construction each period (+0x150).
- `double Growth { get; }`: Population growth of the last period (+8).
- `double[] ImprovementUpkeep { get; }`: Upkeep of this colony's completed research, espionage and construction improvements (+0x180, +0x178, +0x188), by resource.
- `Improvement?[] Improvements { get; }`: One slot per planetary improvement, by its place among them; null where not available (+0x88).
- `int MaxPopulation { get; }`: +4, recomputed when a population improvement completes.
- `Player Owner { get; }`
- `double[] PerPopulation { get; }`: Per-population factor of each resource (+0x38).
- `int Plan { get; }`: Which of the owner's plans orders its improvements (+0x160).
- `Planet Planet { get; }`
- `int PlanetSlot { get; }`
- `double[] Resources { get; }`: Credits, research, espionage, construction and population of the period (+0x10); population is the current one.
- `int Star { get; }`: Index of the planet's star.
- `double StructureUpkeep { get; }`: Upkeep of this colony's structures (+0x190).
- `List<Structure> Structures { get; }`: Structures, the newest kind first (+0x198).
- `void AddTo(StateHash hash)`
- `static bool Breathes(Species species, Planet planet)`: The original's rule by the planet's class, unless a mod's planet type says who breathes it.
- `Structure? FirstOfKind(int kind)`: The first structure of a kind, or null.
- `static int Habitability(Species species, Planet planet)`: 1 for a breathable atmosphere, plus 1 for a tolerable temperature.
- `static int MaximumPopulation(Player player, Planet planet, Colony? colony)`: (rating + 2m) * type + moons * m with m = 1 + habitability, plus the population improvements' flat amount once the colony exists.
- `const int Construction = 3`
- `const int Credits = 0`
- `const int Espionage = 2`
- `const int ImprovementSlots = 50`
- `const int Population = 4`
- `const int ResearchResource = 1`


<a id="type-structure"></a>

### Structure

`class OpenPax.Sim.Players.Structure`

A stack of one to five structures of a kind at a colony: shipyards, missile bases, battle stations and the like.

#### Members

- `new Structure(TechInfo tech, Colony colony)`: A stack of one to five structures of a kind at a colony: shipyards, missile bases, battle stations and the like.
- `Colony Colony { get; }`
- `int Count { get; }`
- `TechInfo? EcmTech { get; }`
- `TechInfo?[] FighterTechs { get; }`
- `double HitPoints { get; }`: A new structure starts all but unbuilt (+0x38).
- `double MaxHitPoints { get; }`: Count times the tech's hit points.
- `int MinesDeployed { get; }`: Minefields: mines laid out in the last battle (+0x8C).
- `double Percent { get; }`
- `double Progress { get; }`: Shipyards: work done on the head of the queue (+0xA8) and its cost (+0xB0).
- `List<Design> Queue { get; }`: Shipyards: ships ordered, the next to finish first (+0xA0).
- `TechInfo? SecondEnergyTech { get; }`: A battle station's second energy weapon slot (+0x80), not read by the strength rating.
- `TechInfo? SensorTech { get; }`
- `double ShieldAbsorbed { get; }`: Damage the shield has soaked up this battle (+0x0C); it regains some each step.
- `TechInfo? ShieldTech { get; }`: Shield, ECM and sensor fitted (+0x60, +0x64, +0x68), for missile bases, battle stations and fighter bases.
- `double[] SubsystemDamage { get; }`: Three counters a hit on a missile base or battle station can raise (+0x48, +0x50, +0x58); meaning not yet known.
- `TechInfo Tech { get; }`
- `double Total { get; }`
- `bool UnderWork { get; }`: +0xB8: set when a unit is added, cleared when building finishes.
- `TechInfo?[] WeaponTechs { get; }`: Two weapons (+0x78, +0x7C) and three fighters (+0x6C to +0x74) fitted.
- `void AddTo(StateHash hash)`
- `const int MaxCount = 5`


<a id="type-fleet"></a>

### Fleet

`class OpenPax.Sim.Players.Fleet`

A fleet: ships, newest first, in one star system, with a queue of orders, an orbit and stats from its ships. See docs/sim/ships.md and docs/sim/movement.md.

#### Members

- `new Fleet(int objectId, string name, Player owner, int star)`: A fleet: ships, newest first, in one star system, with a queue of orders, an orbit and stats from its ships. See docs/sim/ships.md and docs/sim/movement.md.
- `int BoostTicks { get; }`: Ticks of ten times speed left (+0xAC).
- `bool InCombat { get; }`: Held in combat (+0x74 set): the fleet neither checks encounters nor moves.
- `bool Intercepting { get; }`: Set to intercept (+0xA0): toggled by the player's Interdict order. The encounter check (Game.Encounters.cs) sends such a fleet after hostile fleets in its system.
- `int JumpRange { get; }`: Lowest star drive range of the ships.
- `int JumpsLeft { get; }`
- `int JumpsUsed { get; }`: Highest jump count since supply among the ships.
- `FleetMode Mode { get; }`
- `string Name { get; }`
- `int ObjectId { get; }`
- `double OrbitAngle { get; }`
- `ISpaceObject? OrbitCenter { get; }`: What the fleet circles, or null when it sits still or travels.
- `double OrbitSpeed { get; }`
- `List<Order> Orders { get; }`
- `Player Owner { get; }`
- `bool Retreating { get; }`: The flag +0xA8, cleared when the boost runs out; presumed a retreat. Arrival skips combat with a fleet that has it.
- `List<Design> Ships { get; }`
- `double Speed { get; }`: Lowest combat speed of the ships; 0 for none.
- `int Star { get; }`: Index of the star system the fleet is in.
- `int Strength { get; }`
- `int X { get; }`
- `int Y { get; }`
- `void AddTo(StateHash hash)`
- `const int MaxShips = 21`
- `const int OrbitRadius = 30`
- `const double OrbitRate = 50`


<a id="type-design"></a>

### Design

`class OpenPax.Sim.Players.Design`

A ship design, or a ship: the original uses one class for both (copy). A ship is a copy of a design with its own `Design.ShipId`. See docs/sim/ships.md.

#### Members

- `new Design(HullInfo hull)`: A blank design on a hull; no components.
- `Component? Armor { get; }`
- `int BattleCondition { get; }`: Condition percent when its last battle began (+0x38).
- `bool CanAttackPlanets { get; }`: Whether a weapon can strike planets (a planet bomb or an area weapon).
- `string ClassName { get; }`: Class name and number, given when the design goes in a slot.
- `int CombatGroup { get; set; }`: A ship's group in battle, 0 for none, 1 to 9 (+0x54): the battle window selects and sets groups by number keys.
- `IEnumerable<Component> Components { get; }`: Every component in slot order, lists in their order.
- `int Cost { get; }`
- `Component? Ecm { get; }`
- `int ExperienceLevel { get; }`
- `int ExperiencePoints { get; }`
- `IReadOnlyList<Component> Fighters { get; }`: Fighters (and planetary improvements, which share the slot).
- `Fleet? Fleet { get; }`
- `HullInfo Hull { get; }`
- `int Id { get; }`: Design id from the second id counter, given when it goes in a slot; ships keep their design's. -1 for none.
- `bool InBattle { get; }`: Set while the ship is in a battle (+4).
- `int JumpsSinceSupply { get; }`: Wormhole jumps since the ship was last supplied (+0x4C).
- `int MaxStructure { get; }`
- `string Name { get; set; }`: The design's name; the Ship Design window names a design while it is made.
- `Player? Owner { get; }`
- `int Quality { get; }`: Index into the structure factors; the builder sets 0 for type 2, else 4. Meaning not yet certain.
- `int RawSpace { get; }`
- `Design? Refit { get; }`: A ship: the new design it is to be refitted to (+0x8C). On the refitted copy in a shipyard queue: the ship it replaces.
- `Component? Shield { get; }`
- `int ShieldSoaked { get; }`: Shield damage soaked (+0x34), cleared at a battle's start.
- `int ShipId { get; }`: A ship's own id; -1 for a design.
- `int Slot { get; }`
- `IReadOnlyList<Component> Specials { get; }`: Specials such as colonizers and repair systems (slot 8).
- `Component? StarDrive { get; }`
- `int Structure { get; }`
- `Component? TacticalDrive { get; }`
- `Component? Targeting { get; }`
- `int Type { get; }`
- `int UsedSpace { get; }`
- `IReadOnlyList<Component> Weapons { get; }`: Weapons in list order.
- `void Add(TechInfo tech, int mode)`: Fits one of a tech: replaces a single slot, or adds to a list entry of the same tech and mode.
- `void AddExperience(int points)`: Experience points plus a fifth for warriors give levels 0 to 6.
- `void AddTo(StateHash hash)`
- `bool ArcArmed(int mode)`: An arc has firepower at some of its first three bands, or carries a bomb or area weapon.
- `int AutoDamage(Func<int, TechInfo> techById)`: Damage per hit when a battle is resolved at once: for weapon modes 0 to 2 the best of three range bands (undamaged weapons, no bombs), plus fighters, over 4.
- `int BuildCost()`: What building the ship costs: the hull's cost times the quality factor, plus every component's build cost.
- `int CombatAttack()`: Sensor accuracy (undamaged share) plus experience; predators a quarter more.
- `int CombatDefense()`: The average of hull defense and ECM strength.
- `double CombatSpeed()`: The tactical drive's combat speed while undamaged (any damage gives 0: condition / 100 is an integer, a quirk kept), times 1.2 for aquatic species.
- `int ConditionPercent()`: Structure left in percent, integers; 0 for a hull with no structure.
- `Design Copy()`: A copy for a new ship: same components, no experience, no fleet.
- `int DriveRange()`: Wormhole jumps the star drive allows away from supply, scaled by its condition.
- `int EcmRating()`: ECM strength by its condition: what missiles must beat.
- `int Firepower(int mode, int band)`: The damage a firing arc (mode 0 bow, 1 port, 2 starboard, 3 point defense) deals at a range band, from weapons other than planet bombs, times their condition in whole hundreds (so a damaged weapon counts nothing).
- `int HullDefense()`: Hull, experience and drive defense, a tenth more for aquatic species: what beams must beat.
- `void Remove(TechInfo tech, int mode)`: Takes one unit of a tech fitted with a mode away; the entry goes at none. A single slot matches whatever mode it holds.
- `void SetHull(HullInfo hull)`
- `void SetQuality(int quality)`
- `Component? SingleSlot(int slot)`: A single slot's component by slot number: 0 star drive to 5 shield.
- `int Strength(Func<int, TechInfo> techById)`: A strength rating used by diplomacy and computer players.
- `double Upkeep()`: Upkeep a year: the design's cost, times 0.8 for nomads.
- `const int ListSlots = 3`
- `const int SingleSlots = 6`


<a id="type-component"></a>

### Component

`class OpenPax.Sim.Players.Component`

Some number of one tech fitted to a design or ship.

| Parameter | Meaning |
|---|---|
| `mode` | Weapon mode, the firing arc: 0 bow, 1 port, 2 starboard, 3 point defense; -1 for anything not a weapon. |

#### Members

- `new Component(TechInfo tech, int mode)`: Some number of one tech fitted to a design or ship.
  - `mode`: Weapon mode, the firing arc: 0 bow, 1 port, 2 starboard, 3 point defense; -1 for anything not a weapon.
- `int Condition { get; }`: 100 when undamaged, integer arithmetic.
- `int Count { get; }`
- `int Damage { get; }`: Damage taken, against count times the tech's hit points.
- `int Mode { get; }`
- `int Operational { get; }`: How many still work.
- `bool Ready { get; }`: Ready to fire: loaded, a weapon, and working.
- `int ReloadLeft { get; }`: Fire phases until it can fire again (+0x14).
- `TechInfo Tech { get; }`
- `int VolleyLeft { get; }`: Rounds left in the volley being fired (+0x1C): the full count when a battle starts, then the working count at each reload.
- `void AddTo(StateHash hash)`
- `Component Copy()`
- `const int NotWeapon = -1`


<a id="type-species"></a>

### Species

`record OpenPax.Sim.Players.Species`

A species as the simulation uses it (the original's race template; see docs/formats/tables.md). The caller converts the data records.

#### Members

- `new Species(int RaceId, string Name, int Picture, int PreferredTemperature, int TemperatureTolerance, IReadOnlyList<bool> Atmospheres, IReadOnlyList<bool> PhysicalTraits, IReadOnlyList<bool> CulturalTraits, IReadOnlyList<sbyte> RateModifiers, IReadOnlyList<sbyte> ResearchLevels)`: A species as the simulation uses it (the original's race template; see docs/formats/tables.md). The caller converts the data records.
- `IReadOnlyList<bool> Atmospheres { get; init; }`
- `IReadOnlyList<bool> CulturalTraits { get; init; }`
- `string Name { get; init; }`
- `IReadOnlyList<bool> PhysicalTraits { get; init; }`
- `int Picture { get; init; }`
- `int PreferredTemperature { get; init; }`
- `int RaceId { get; init; }`
- `IReadOnlyList<sbyte> RateModifiers { get; init; }`
- `IReadOnlyList<sbyte> ResearchLevels { get; init; }`
- `int TemperatureTolerance { get; init; }`
- `bool Breathes(int atmosphereClass)`: Whether the species can live in an atmosphere class.
- `bool Has(int physicalTrait)`
- `bool Is(int culturalTrait)`
- `bool Tolerates(int temperature)`: Whether a temperature suits the species.
- `const int Anaerobic = 0`
- `const int Aquatic = 4`
- `const int Builder = 4`
- `const int Merchant = 6`
- `const int Nomad = 5`
- `const int Politician = 2`
- `const int Predator = 3`
- `const int Psychic = 2`
- `const int Scientist = 1`
- `const int Spy = 3`
- `const int Subterranean = 1`
- `const int Warrior = 0`


<a id="type-incidentkind"></a>

### IncidentKind

`enum OpenPax.Sim.Players.IncidentKind`

The kinds of event that move attitude scores (docs/sim/diplomacy.md).

| Value | | Meaning |
|---|---|---|
| `TradeRoutes` | 0 |  |
| `JointResearch` | 1 |  |
| `Disposition` | 2 |  |
| `Stronger` | 3 |  |
| `EnemyAttacked` | 4 |  |
| `FriendAttacked` | 5 |  |
| `SpyCaught` | 6 |  |
| `ColonyTaken` | 7 |  |
| `ShipDestroyed` | 8 |  |
| `StructureDestroyed` | 9 |  |
| `PlanetAttacked` | 10 |  |
| `FleetAttacked` | 11 |  |
| `ShipsGiven` | 12 |  |
| `ShipsReceived` | 13 |  |
| `PlanetsGiven` | 14 |  |
| `PlanetsReceived` | 15 |  |
| `TechsGiven` | 16 |  |
| `TechsReceived` | 17 |  |
| `CreditsGiven` | 18 |  |
| `CreditsReceived` | 19 |  |
| `MapGiven` | 20 |  |
| `MapReceived` | 21 |  |
| `TradeRejected` | 22 |  |

## Game tables

The data every game is made from, which ChangeTables may change.

<a id="type-gametables"></a>

### GameTables

`record OpenPax.Sim.Setup.GameTables`

Game data a new game reads, supplied by the caller from the data files so the simulation does not depend on file formats.

| Parameter | Meaning |
|---|---|
| `Species` | The RACE records in resource order; AIs pick by index. |
| `AiNames` | STR# 46000, 30 computer player names. |
| `FleetNameText` | STR# 108 entry 10, put between the star name and the fleet number. |
| `Techs` | Every technology, by id order. |
| `Hulls` | Hulls by type 0 to 5. |
| `DesignNames` | STR# 26000; a design of type t is named by entry t + 27. |
| `ClassNames` | STR# 20005; entry t names the class of a type t design. |
| `Sin` | Sine of each whole degree as the original's table holds it (docs/sim/movement.md). |
| `Cos` | Cosine likewise. |
| `StolenFleetName` | STR# 112 entry 6, the name of a fleet of stolen ships. |
| `TradedFleetName` | STR# 82 entry 7, the name of a fleet of ships received in trade. |
| `AbandonFleetName` | STR# 82 entry 6, the name of the fleet a nomad species leaves an abandoned colony in. |
| `TransportName` | STR# 100 entry 2, the name of a colony ship design made for abandoning a colony. |
| `CombatSpriteRows` | Last visible row of each Image/Combat.img picture: the size of orbital defenses in battle. |

#### Members

- `new GameTables(IReadOnlyList<Species> Species, IReadOnlyList<string> AiNames, NameTables GalaxyNames, string FleetNameText, IReadOnlyList<TechInfo> Techs, IReadOnlyList<HullInfo> Hulls, IReadOnlyList<string> DesignNames, IReadOnlyList<string> ClassNames, IReadOnlyList<double> Sin, IReadOnlyList<double> Cos, IReadOnlyDictionary<int, int> CombatSpriteRows, string StolenFleetName, string TradedFleetName, string AbandonFleetName, string TransportName)`: Game data a new game reads, supplied by the caller from the data files so the simulation does not depend on file formats.
  - `Species`: The RACE records in resource order; AIs pick by index.
  - `AiNames`: STR# 46000, 30 computer player names.
  - `FleetNameText`: STR# 108 entry 10, put between the star name and the fleet number.
  - `Techs`: Every technology, by id order.
  - `Hulls`: Hulls by type 0 to 5.
  - `DesignNames`: STR# 26000; a design of type t is named by entry t + 27.
  - `ClassNames`: STR# 20005; entry t names the class of a type t design.
  - `Sin`: Sine of each whole degree as the original's table holds it (docs/sim/movement.md).
  - `Cos`: Cosine likewise.
  - `StolenFleetName`: STR# 112 entry 6, the name of a fleet of stolen ships.
  - `TradedFleetName`: STR# 82 entry 7, the name of a fleet of ships received in trade.
  - `AbandonFleetName`: STR# 82 entry 6, the name of the fleet a nomad species leaves an abandoned colony in.
  - `TransportName`: STR# 100 entry 2, the name of a colony ship design made for abandoning a colony.
  - `CombatSpriteRows`: Last visible row of each Image/Combat.img picture: the size of orbital defenses in battle.
- `string AbandonFleetName { get; init; }`: STR# 82 entry 6, the name of the fleet a nomad species leaves an abandoned colony in.
- `IReadOnlyList<string> AiNames { get; init; }`: STR# 46000, 30 computer player names.
- `IReadOnlyList<string> ClassNames { get; init; }`: STR# 20005; entry t names the class of a type t design.
- `IReadOnlyDictionary<int, (int, int)> CombatPictureSizes { get; init; }`: Width and height of each Image/Combat.img picture: where a planet's blocks stand in battle.
- `IReadOnlyDictionary<int, int> CombatSpriteRows { get; init; }`: Last visible row of each Image/Combat.img picture: the size of orbital defenses in battle.
- `IReadOnlyList<double> Cos { get; init; }`: Cosine likewise.
- `IReadOnlyList<string> DesignNames { get; init; }`: STR# 26000; a design of type t is named by entry t + 27.
- `IReadOnlyList<DesignTemplate> Designs { get; init; }`: Premade ship designs mods add, given to human players once they know every part.
- `string FleetNameText { get; init; }`: STR# 108 entry 10, put between the star name and the fleet number.
- `NameTables GalaxyNames { get; init; }`
- `IReadOnlyList<HullInfo> Hulls { get; init; }`: Hulls by type 0 to 5.
- `IReadOnlyList<PlanetKind> PlanetKinds { get; init; }`: Planet types mods add (docs/modding/content.md); a planet's Kind is its place here plus one. Empty without such mods.
- `IReadOnlyList<double> Sin { get; init; }`: Sine of each whole degree as the original's table holds it (docs/sim/movement.md).
- `IReadOnlyList<Species> Species { get; init; }`: The RACE records in resource order; AIs pick by index.
- `string StolenFleetName { get; init; }`: STR# 112 entry 6, the name of a fleet of stolen ships.
- `IReadOnlyList<TechInfo> Techs { get; init; }`: Every technology, by id order.
- `string TradedFleetName { get; init; }`: STR# 82 entry 7, the name of a fleet of ships received in trade.
- `string TransportName { get; init; }`: STR# 100 entry 2, the name of a colony ship design made for abandoning a colony.
- `int IndexInKind(TechInfo tech)`: A tech's place among the techs of its kind, from 0: what the original reads as a mine field's or minesweeper's level, or an improvement's slot.
- `IEnumerable<TechInfo> OfKind(int kind)`: The techs of one kind in id order. The original walks from the first of the kind while the kind matches; techs of a kind are contiguous in its data, so taking every tech of the kind gives the same list, and also takes in mods' techs, which come after.
- `TechInfo Tech(int id)`: A tech by id (ids run from 1 in table order).


<a id="type-gamesetup"></a>

### GameSetup

`record OpenPax.Sim.Setup.GameSetup`

A new game's settings (the original's setup structure, see docs/sim/universe.md). Humans come first in the player order; the rest of `GameSetup.TotalPlayers` are computer players.

| Parameter | Meaning |
|---|---|
| `Difficulty` | 1 (easiest) to 5. |

#### Members

- `new GameSetup(uint Seed, int StarCount, int TotalPlayers, int Difficulty, IReadOnlyList<HumanPlayerSetup> Humans)`: A new game's settings (the original's setup structure, see docs/sim/universe.md). Humans come first in the player order; the rest of `GameSetup.TotalPlayers` are computer players.
  - `Difficulty`: 1 (easiest) to 5.
- `int Difficulty { get; init; }`: 1 (easiest) to 5.
- `IReadOnlyList<HumanPlayerSetup> Humans { get; init; }`
- `uint Seed { get; init; }`
- `int StarCount { get; init; }`
- `int TotalPlayers { get; init; }`


<a id="type-techinfo"></a>

### TechInfo

`record OpenPax.Sim.Players.TechInfo`

The parts of a technology the simulation reads (see OpenPax.Data TECH records and docs/sim/ships.md). Class fields are 0 for other classes.

| Parameter | Meaning |
|---|---|
| `Kind` | Role of the tech; for example 5 shipyard, 7 missile base, 24 armor. |

#### Members

- `new TechInfo(int Id, int Kind, int ResearchCost)`: The parts of a technology the simulation reads (see OpenPax.Data TECH records and docs/sim/ships.md). Class fields are 0 for other classes.
  - `Kind`: Role of the tech; for example 5 shipyard, 7 missile base, 24 armor.
- `int ArmorHullLimit { get; init; }`: Armor: the byte before the multiplier; computer players use a design only if its hull type is at most this. Meaning not yet certain.
- `int BuildCost { get; init; }`: What building one costs in construction (TECH +0x20 in the original's view).
- `int ClassId { get; init; }`: TECH class 6001 to 6010.
- `int CombatSpeed { get; init; }`: Tactical drives.
- `int Cost { get; init; }`: Per-component cost, scaled by tech level.
- `IReadOnlyList<int> Damage { get; init; }`: Weapons: damage in the four range bands.
- `int DefenseBonus { get; init; }`
- `int DesignSlot { get; }`: The design slot a component of this tech goes in.
- `int DriveRating { get; init; }`: Star drives: wormhole jumps away from supply.
- `int EcmStrength { get; init; }`: ECM: strength.
- `int FighterAccuracy { get; init; }`: Fighters: the accuracy of their shots (+0x52).
- `int FighterDefense { get; init; }`: Fighters: taken off a beam's chance to hit (+0x4E).
- `int FighterHitPoints { get; init; }`: Fighters: hit points of a flight; each of its three craft has a third (+0x50).
- `int FighterMissileDefense { get; init; }`: Fighters: taken off a missile's tracking (+0x54).
- `int FighterSpeed { get; init; }`: Fighters: speed in battle (+0x48).
- `int FighterWeapon1 { get; init; }`: Fighters: the two weapon tech ids (0 for none).
- `int FighterWeapon2 { get; init; }`
- `IReadOnlyList<double> Flat { get; init; }`: Planetary improvements: flat amount of each resource.
- `int HitPoints { get; init; }`
- `int Id { get; init; }`
- `int InstallationLevel { get; init; }`: Installations and specials: level 1 to 5 (also repair amount; a minefield's mine sensor range).
- `int InstallationStrength { get; init; }`: Installations: 100 or 500, else 0; a minefield's mine damage and hit points.
- `int Kind { get; init; }`: Role of the tech; for example 5 shipyard, 7 missile base, 24 armor.
- `int Look { get; init; }`: The port's own, for mods' techs (docs/modding/content.md): the tech whose place among its kind gives this one's looks and place-based roles (a fighter's art and base slot, a colonizer's generation). 0 for the game's own techs, which are their own.
- `int LookId { get; }`: The tech this one looks like: `TechInfo.Look`, or itself.
- `int MaxHullClass { get; init; }`: Star drives: largest hull class moved (2, 3 or 5).
- `int MissileHitPoints { get; init; }`: A missile's hit points against point defense (+0x4C).
- `IReadOnlyList<double> PerPopulation { get; init; }`: Planetary improvements: per-population amount of credits, research, espionage, construction, population.
- `int Picture { get; init; }`: The first of the tech's 36 animation pictures in Tech.img; its 50x50 icon is the one before.
- `IReadOnlyList<int> Prerequisites { get; init; }`: Four tech ids needed first; 0 for none.
- `int Reload { get; init; }`
- `int ResearchCost { get; init; }`
- `int ResearchField { get; }`: Research field by class: 0 weapons, 1 shielding, 2 ship, 3 colonial, 4 space.
- `int ResearchGroup { get; init; }`: The line of the research tree it belongs to (STR# 105 names them); 0 for theories.
- `int Resource { get; }`: The one resource an improvement works on: the first with a flat value, else the first with a per-population value, else credits.
- `int SensorAccuracy { get; init; }`: Targeting sensors: accuracy bonus.
- `int SensorRange { get; init; }`: A targeting sensor's range (+0x48) and how many targets it tracks (+0x4C).
- `int SensorTargets { get; init; }`
- `int ShieldCapacity { get; init; }`
- `int ShieldPercent { get; init; }`
- `int ShieldRegeneration { get; init; }`: Shields: capacity regained each combat step.
- `int ShotSpeed { get; init; }`: A shot's top speed per step (+0x64 doubled).
- `int Size { get; init; }`: Space used in a design, scaled by tech level.
- `double StructureMultiplier { get; init; }`: Armor: multiplies a design's structure.
- `bool Tracks { get; }`: A weapon's shots home on their target (tracking byte not 255): missiles.
- `int WeaponAccuracy { get; init; }`: A weapon's accuracy bonus (+0x58), added to its shots' chance to hit.
- `int WeaponEffect { get; init; }`: Weapons: which picture set and sound its shots have in battle (+0x48).
- `int WeaponFlag { get; init; }`: Weapons: the byte before the weapon type (255 for non-missiles); battle stations take only weapons with 255. Meaning not yet certain.
- `int WeaponType { get; init; }`: Weapons: 0 normal, 1 planet bomb, 2 area.
- `const int AreaWeaponKind = 19`
- `const int ArmorKind = 24`
- `const int BattleStationKind = 8`
- `const int ColonizerKind = 2`
- `const int EcmKind = 14`
- `const int EnergyWeaponKind = 15`
- `const int FieldKind = 22`
- `const int FighterBaseKind = 9`
- `const int FighterKind = 23`
- `const int ImprovementClass = 6010`
- `const int ImprovementKind = -2`
- `const int MineFieldKind = 10`
- `const int MinesweeperKind = 3`
- `const int MissileBaseKind = 7`
- `const int MissileKind = 17`
- `const int PlanetBombKind = 18`
- `const int ProjectileKind = 16`
- `const int ScreenKind = 20`
- `const int SensorKind = 13`
- `const int ShieldKind = 21`
- `const int ShipyardKind = 5`
- `const int SpecialClass = 6009`
- `const int StarDriveKind = 11`
- `const int TacticalDriveKind = 12`
- `const int TrainingBaseKind = 6`


<a id="type-hullinfo"></a>

### HullInfo

`record OpenPax.Sim.Players.HullInfo`

A hull (HULL 2000 + type) as designs read it.

#### Members

- `new HullInfo(int Type, int Structure, int Capacity, int Cost)`: A hull (HULL 2000 + type) as designs read it.
- `int Capacity { get; init; }`
- `int Cost { get; init; }`
- `int Structure { get; init; }`
- `int Type { get; init; }`


## Loading (the game's side)

How the game finds and loads mods; a mod does not call these.

<a id="type-modhost"></a>

### ModHost

`class OpenPax.Modding.ModHost`

The mods turned on, loaded once when the game starts (docs/modding.md): their data packs over the game's data, and their C# assemblies, each in its own load context sharing the game's own assemblies.

#### Members

- `IReadOnlyList<string> Errors { get; }`: What went wrong finding or loading mods, for the player to be told.
- `string Fingerprint { get; }`: The mod set as the lobby and the game server compare it: each mod's key with its files' hash ("id@version#hash", ModKey), in order, joined by commas; empty with none.
- `IReadOnlyList<string> Messages { get; }`: What the mods logged while loading (ModContext.Log), each line under its mod's id.
- `IReadOnlyList<ModInfo> Mods { get; }`: The mods loaded, in their order; a later one wins where two replace the same.
- `static ModHost None { get; }`: No mods.
- `void Attach(Game game)`: A game run here (new or loaded): the mods' step rules act after each of its steps.
- `GameTables ChangeTables(GameTables tables)`: The tables as the mods change them, in their order.
- `DataFiles Data(string baseFolder)`: The game's data with the mods' data packs over it, the last mod winning.
- `static ModHost Load(string modsFolder, IReadOnlyList<string> enabled)`: Loads the mods turned on, in the order given, from those found in the mods folder. A mod that cannot be loaded is left out and said in the errors; the rest load.
- `static ModHost Load(IReadOnlyList<string> modsFolders, IReadOnlyList<string> enabled)`: As `ModHost.Load`, from several mods folders, the first one winning (ModCatalog.Find).
- `static ModHost Load(IReadOnlyList<string> modsFolders, IReadOnlyList<ModKey> set, ref IReadOnlyList<ModKey> missing)`: Loads a mod set by its keys (a room's, docs/modding/multiplayer.md), each from wherever a mod with that id, version and hash is found in the folders. Those not found are given back in missing, and the rest load.
- `void NewGame(Game game)`: A new game: it records the mods, then each mod acts on it.
- `void Unload()`: Lets the mods' assemblies go, before another set is loaded in their place (docs/modding/multiplayer.md). Nothing of theirs may be in use: no game they were attached to may run on.


<a id="type-modcatalog"></a>

### ModCatalog

`static class OpenPax.Modding.ModCatalog`

The mods in the mods folders: each a folder with a mod.toml.

#### Members

- `static IReadOnlyList<ModInfo> Find(string modsFolder, ICollection<string> errors)`: Every readable mod, by folder name order; what could not be read is said in the errors.
- `static IReadOnlyList<ModInfo> Find(IReadOnlyList<string> modsFolders, ICollection<string> errors, bool keepDuplicates = False)`: Every readable mod in several mods folders, the first folder first. A mod in an earlier folder hides one with the same id in a later one, so the player's own copy of a mod the game ships with is the one used.
  - `keepDuplicates`: Keep every copy of an id, from every folder, first folder first: to find one by its hash (a room's mod set).

