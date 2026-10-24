using OpenPax.Sim;
using OpenPax.Sim.Galaxy;
using OpenPax.Sim.Players;
using OpenPax.Sim.Setup;

namespace OpenPax.Modding;

/// <summary>
/// What a C# mod may change (docs/modding.md), given to <see cref="IMod.Load"/>.
/// A mod's rules run where the game is run: in single player, or on the
/// server of a network game, whose players all hold the same mods (the
/// lobby sees to it), so a game stays one game.
/// </summary>
public sealed class ModContext
{
    internal ModContext(ModInfo mod)
    {
        Mod = mod;
    }

    /// <summary>The mod's manifest and folder.</summary>
    public ModInfo Mod { get; }

    internal List<Func<GameTables, GameTables>> TableChanges { get; } = [];

    internal List<Action<Game>> NewGameActions { get; } = [];

    internal List<Action<Game>> StepActions { get; } = [];

    internal List<Action<Game, string?>> LoadedGameActions { get; } = [];

    internal List<Func<Game, Fleet, ISpaceObject, bool>> BattleHandlers { get; } = [];

    internal List<Func<Game, Colony, ColonyRating?>> Ratings { get; } = [];

    internal List<Func<Game, Fleet, ISpaceObject, double?>> AttackOdds { get; } = [];

    internal List<Action<Game, ModEvent>> EventHandlers { get; } = [];

    internal List<string> LogLines { get; } = [];

    /// <summary>The mod's id, which prefixes its keys in a game's mod state.</summary>
    public string Id => Mod.Manifest.Id;

    /// <summary>The mod's own saved values in a game (ModStore): saved, sent to clients and hashed.</summary>
    public ModStore Store(Game game) => new(game, Id);

    /// <summary>The mod's own random numbers in a game (ModRandom), deterministic and saved with it.</summary>
    public ModRandom Random(Game game) => new(game, Id);

    /// <summary>
    /// A TOML file of the mod's folder, read once at load (its settings, for
    /// one): keys to values (long, double, bool, string, lists, tables). An
    /// empty table when the file is missing; a file that does not read is
    /// logged and empty.
    /// </summary>
    public IReadOnlyDictionary<string, object> ReadToml(string fileName)
    {
        var path = Path.Combine(Mod.Folder, fileName);
        if (!File.Exists(path))
        {
            return new Dictionary<string, object>();
        }

        try
        {
            return OpenPax.Data.Toml.TomlReader.Parse(File.ReadAllText(path));
        }
        catch (Exception e) when (e is FormatException or IOException or UnauthorizedAccessException)
        {
            Log(fileName + ": " + e.Message);
            return new Dictionary<string, object>();
        }
    }

    /// <summary>A line for the player to see when the game starts, and on the console: what the mod wants known.</summary>
    public void Log(string line) => LogLines.Add(Id + ": " + line);

    /// <summary>Changes the game tables (techs, hulls, species, names) every game is made from; mods change them in their order.</summary>
    public void ChangeTables(Func<GameTables, GameTables> change) => TableChanges.Add(change);

    /// <summary>Acts on a new game once it is made, before its first step (not on a loaded one).</summary>
    public void OnNewGame(Action<Game> action) => NewGameActions.Add(action);

    /// <summary>
    /// Acts on a saved game as it is loaded, given the mod's version the game
    /// was saved with: null when the mod was not in it then (docs/modding/lua.md,
    /// Saves). A newer version brings its saved values up to date here.
    /// </summary>
    public void OnLoadedGame(Action<Game, string?> action) => LoadedGameActions.Add(action);

    /// <summary>Acts after every step of a game, new or loaded, before the step's state hash.</summary>
    public void OnStep(Action<Game> action) => StepActions.Add(action);

    /// <summary>
    /// Has a say over each battle about to begin (<see cref="Game.TakeOverBattle"/>):
    /// given the attacker and its target, true takes it over and no battle opens.
    /// The first mod in order to take a battle over has it.
    /// </summary>
    public void OnBattle(Func<Game, Fleet, ISpaceObject, bool> handler) => BattleHandlers.Add(handler);

    /// <summary>
    /// Gives a colony's attack and defense for the planet lists to show
    /// (<see cref="Game.RateColony"/>); null leaves it to the next mod. The
    /// first mod in order to rate a colony has it.
    /// </summary>
    public void OnColonyRating(Func<Game, Colony, ColonyRating?> rating) => Ratings.Add(rating);

    /// <summary>
    /// Gives the chance, 0 to 1, that a fleet's attack on a fleet or a planet
    /// succeeds (<see cref="Game.RateAttack"/>), shown beside the pointer; null
    /// leaves it to the next mod.
    /// </summary>
    public void OnAttackOdds(Func<Game, Fleet, ISpaceObject, double?> odds) => AttackOdds.Add(odds);

    /// <summary>Hears every event of a game (<see cref="Game.Happened"/>) as it happens, for every player.</summary>
    public void OnEvent(Action<Game, ModEvent> handler) => EventHandlers.Add(handler);

    /// <summary>Hears the events of one kind.</summary>
    public void OnEvent(ModEventKind kind, Action<Game, ModEvent> handler) => EventHandlers.Add((g, e) =>
    {
        if (e.Kind == kind)
        {
            handler(g, e);
        }
    });

    /// <summary>A player learned a tech.</summary>
    public void OnTechLearned(Action<Game, Player, TechInfo> handler) => OnEvent(ModEventKind.TechLearned, (g, e) => handler(g, e.Player, e.Tech!)); // Tech is set for this kind

    /// <summary>A ship was built, at the planet given.</summary>
    public void OnShipBuilt(Action<Game, Design, Planet> handler) => OnEvent(ModEventKind.ShipBuilt, (g, e) => handler(g, e.Ship!, (Planet)e.Place!)); // Ship and the planet are set for this kind

    /// <summary>A ship was destroyed; the player that did it when known.</summary>
    public void OnShipDestroyed(Action<Game, Design, Player, Player?> handler) => OnEvent(ModEventKind.ShipDestroyed, (g, e) => handler(g, e.Ship!, e.Player, e.Other)); // Ship is set for this kind

    /// <summary>A colony was founded (not one changing hands).</summary>
    public void OnColonyFounded(Action<Game, Player, Planet> handler) => OnEvent(ModEventKind.ColonyFounded, (g, e) => handler(g, e.Player, (Planet)e.Place!)); // the planet is set for this kind

    /// <summary>A colony went from one player (the third argument) to another (the second).</summary>
    public void OnColonyChangedHands(Action<Game, Player, Player, Planet> handler) => OnEvent(ModEventKind.ColonyChangedHands, (g, e) => handler(g, e.Player, e.Other!, (Planet)e.Place!)); // both players and the planet are set for this kind

    /// <summary>A colony was destroyed or abandoned (not one changing hands).</summary>
    public void OnColonyDestroyed(Action<Game, Player, Planet> handler) => OnEvent(ModEventKind.ColonyDestroyed, (g, e) => handler(g, e.Player, (Planet)e.Place!)); // the planet is set for this kind

    /// <summary>A treaty slot changed: from, toward, slot (Treaty.War and on), new value (Treaty.Off and on).</summary>
    public void OnTreatyChanged(Action<Game, Player, Player, int, int> handler) => OnEvent(ModEventKind.TreatyChanged, (g, e) => handler(g, e.Player, e.Other!, e.State, e.Value)); // Other is set for this kind

    /// <summary>A player was eliminated.</summary>
    public void OnPlayerEliminated(Action<Game, Player> handler) => OnEvent(ModEventKind.PlayerEliminated, (g, e) => handler(g, e.Player));
}
