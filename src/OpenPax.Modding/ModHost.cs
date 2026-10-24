using System.Globalization;
using System.Reflection;
using System.Runtime.Loader;
using OpenPax.Data;
using OpenPax.Sim;
using OpenPax.Sim.Setup;

namespace OpenPax.Modding;

/// <summary>
/// The mods turned on, loaded once when the game starts (docs/modding.md):
/// their data packs over the game's data, and their C# assemblies, each in
/// its own load context sharing the game's own assemblies.
/// </summary>
public sealed class ModHost
{
    private readonly List<ModContext> _contexts = [];
    private readonly List<AssemblyLoadContext> _loadContexts = [];
    private readonly List<string> _errors;

    private ModHost(IReadOnlyList<ModInfo> mods, List<string> errors)
    {
        Mods = mods;
        _errors = errors;
    }

    /// <summary>The mods loaded, in their order; a later one wins where two replace the same.</summary>
    public IReadOnlyList<ModInfo> Mods { get; }

    /// <summary>What went wrong finding or loading mods, for the player to be told.</summary>
    public IReadOnlyList<string> Errors => _errors;

    /// <summary>
    /// The mod set as the lobby and the game server compare it: each mod's
    /// key with its files' hash ("id@version#hash", ModKey), in order, joined
    /// by commas; empty with none.
    /// </summary>
    public string Fingerprint => ModKey.Join(Mods.Select(m => m.Key));

    /// <summary>No mods.</summary>
    public static ModHost None { get; } = new([], []);

    /// <summary>
    /// Loads the mods turned on, in the order given, from those found in the
    /// mods folder. A mod that cannot be loaded is left out and said in the
    /// errors; the rest load.
    /// </summary>
    public static ModHost Load(string modsFolder, IReadOnlyList<string> enabled) => Load([modsFolder], enabled);

    /// <summary>As <see cref="Load(string, IReadOnlyList{string})"/>, from several mods folders, the first one winning (ModCatalog.Find).</summary>
    public static ModHost Load(IReadOnlyList<string> modsFolders, IReadOnlyList<string> enabled)
    {
        var errors = new List<string>();
        var found = ModCatalog.Find(modsFolders, errors);
        var mods = new List<ModInfo>();
        foreach (var id in enabled)
        {
            if (found.FirstOrDefault(m => m.Manifest.Id.Equals(id, StringComparison.OrdinalIgnoreCase)) is { } mod)
            {
                mods.Add(mod);
            }
            else
            {
                errors.Add(string.Format(CultureInfo.InvariantCulture, Strings.NotFound, id));
            }
        }

        return Start(mods, errors);
    }

    /// <summary>
    /// Loads a mod set by its keys (a room's, docs/modding/multiplayer.md),
    /// each from wherever a mod with that id, version and hash is found in the
    /// folders. Those not found are given back in missing, and the rest load.
    /// </summary>
    public static ModHost Load(IReadOnlyList<string> modsFolders, IReadOnlyList<ModKey> set, out IReadOnlyList<ModKey> missing)
    {
        var errors = new List<string>();
        var found = ModCatalog.Find(modsFolders, errors, keepDuplicates: true);
        var mods = new List<ModInfo>();
        var lacking = new List<ModKey>();
        foreach (var key in set)
        {
            if (found.FirstOrDefault(key.Matches) is { } mod)
            {
                mods.Add(mod);
            }
            else
            {
                lacking.Add(key);
            }
        }

        missing = lacking;
        return Start(mods, errors);
    }

    private static ModHost Start(List<ModInfo> mods, List<string> errors)
    {
        var host = new ModHost(mods, errors);
        foreach (var mod in mods.ToList())
        {
            if (!host.LoadCode(mod))
            {
                mods.Remove(mod);
            }
        }

        return host;
    }

    /// <summary>
    /// Lets the mods' assemblies go, before another set is loaded in their
    /// place (docs/modding/multiplayer.md). Nothing of theirs may be in use:
    /// no game they were attached to may run on.
    /// </summary>
    public void Unload()
    {
        _contexts.Clear();
        foreach (var context in _loadContexts)
        {
            context.Unload();
        }

        _loadContexts.Clear();
    }

    /// <summary>The game's data with the mods' data packs over it, the last mod winning.</summary>
    public DataFiles Data(string baseFolder) => new(baseFolder, Mods.Reverse().Select(m => m.DataFolder).OfType<string>());

    /// <summary>The tables as the mods change them, in their order.</summary>
    public GameTables ChangeTables(GameTables tables) =>
        _contexts.SelectMany(c => c.TableChanges).Aggregate(tables, (t, change) => change(t));

    /// <summary>A new game: it records the mods, then each mod acts on it.</summary>
    public void NewGame(Game game)
    {
        game.Mods.Clear();
        game.Mods.AddRange(Mods.Select(m => m.Manifest.Key));
        foreach (var action in _contexts.SelectMany(c => c.NewGameActions))
        {
            action(game);
        }

        Attach(game);
    }

    /// <summary>What the mods logged while loading (ModContext.Log), each line under its mod's id.</summary>
    public IReadOnlyList<string> Messages => [.. _contexts.SelectMany(c => c.LogLines)];

    /// <summary>
    /// A saved game loaded here (docs/modding/lua.md, Saves): each mod turned
    /// on hears the version it was saved with (null when it was not in the
    /// game) and may bring its saved values up to date; the players are told
    /// what changed. A mod the save had that is not turned on keeps its
    /// record and its saved values, for when it is turned on again. Then the
    /// mods attach as to any game.
    /// </summary>
    public void Resume(Game game)
    {
        var saved = game.Mods.Select(SplitKey).ToList();
        var notes = new List<string>();
        foreach (var context in _contexts)
        {
            var manifest = context.Mod.Manifest;
            var was = saved.FirstOrDefault(s => s.Id.Equals(manifest.Id, StringComparison.OrdinalIgnoreCase));
            if (was.Id is null)
            {
                notes.Add(string.Format(CultureInfo.InvariantCulture, Strings.SaveModAdded, manifest.Name));
            }
            else if (was.Version != manifest.Version)
            {
                notes.Add(string.Format(CultureInfo.InvariantCulture, Strings.SaveModChanged, manifest.Name, was.Version, manifest.Version));
            }

            foreach (var action in context.LoadedGameActions)
            {
                action(game, was.Version);
            }
        }

        var missing = saved.Where(s => !Mods.Any(m => m.Manifest.Id.Equals(s.Id, StringComparison.OrdinalIgnoreCase))).ToList();
        foreach (var gone in missing)
        {
            notes.Add(string.Format(CultureInfo.InvariantCulture, Strings.SaveModMissing, gone.Key));
        }

        // The game now records the mods it plays with, then the ones it keeps state for.
        game.Mods.Clear();
        game.Mods.AddRange(Mods.Select(m => m.Manifest.Key));
        game.Mods.AddRange(missing.Select(m => m.Key));
        foreach (var player in game.Players.Where(p => !p.IsAi))
        {
            foreach (var note in notes)
            {
                game.Tell(player, note);
            }
        }

        Attach(game);
    }

    // "id@version" as the game records a mod; a key with no version has none.
    private static (string Id, string? Version, string Key) SplitKey(string key)
    {
        var at = key.LastIndexOf('@');
        return at < 0 ? (key, null, key) : (key[..at], key[(at + 1)..], key);
    }

    /// <summary>A game run here (new or loaded): the mods' step rules act after each of its steps.</summary>
    public void Attach(Game game)
    {
        foreach (var handler in _contexts.SelectMany(c => c.EventHandlers))
        {
            game.Happened += handler;
        }

        foreach (var action in _contexts.SelectMany(c => c.StepActions))
        {
            game.Stepped += action;
        }

        var battles = _contexts.SelectMany(c => c.BattleHandlers).ToList();
        if (battles.Count > 0)
        {
            game.TakeOverBattle = (g, attacker, target) => battles.Any(h => h(g, attacker, target));
        }

        var ratings = _contexts.SelectMany(c => c.Ratings).ToList();
        if (ratings.Count > 0)
        {
            game.RateColony = (g, colony) => ratings.Select(r => r(g, colony)).FirstOrDefault(r => r is not null);
        }

        var odds = _contexts.SelectMany(c => c.AttackOdds).ToList();
        if (odds.Count > 0)
        {
            game.RateAttack = (g, fleet, target) => odds.Select(o => o(g, fleet, target)).FirstOrDefault(o => o is not null);
        }
    }

    private bool LoadCode(ModInfo mod)
    {
        if (mod.Manifest.Script is { } script)
        {
            var scripted = new ModContext(mod);
            if (!Lua.LuaModScript.Load(mod, script, scripted, _errors))
            {
                return false;
            }

            _contexts.Add(scripted);
            return true;
        }

        if (mod.AssemblyPath is not { } path)
        {
            _contexts.Add(new ModContext(mod));
            return true;
        }

        var id = mod.Manifest.Id;
        try
        {
            var loadContext = new ModLoadContext(path);
            _loadContexts.Add(loadContext);
            var assembly = loadContext.Load(Path.GetFullPath(path));
            var entries = assembly.GetExportedTypes().Where(t => typeof(IMod).IsAssignableFrom(t) && t is { IsAbstract: false, IsInterface: false }).ToList();
            if (entries.Count == 0)
            {
                _errors.Add(string.Format(CultureInfo.InvariantCulture, Strings.NoEntry, id));
                return false;
            }

            var context = new ModContext(mod);
            foreach (var type in entries)
            {
                ((IMod)Activator.CreateInstance(type)!).Load(context); // Not null: a class with a public parameterless constructor makes an instance.
            }

            _contexts.Add(context);
            return true;
        }
        catch (Exception e) when (e is IOException or BadImageFormatException or FileLoadException or ReflectionTypeLoadException or MissingMethodException or TypeLoadException)
        {
            _errors.Add(string.Format(CultureInfo.InvariantCulture, Strings.AssemblyFailed, id, path, e.Message));
            return false;
        }
#pragma warning disable CA1031 // A mod is someone else's code: whatever it throws while loading leaves it out, rather than stopping the game.
        catch (Exception e)
#pragma warning restore CA1031
        {
            _errors.Add(string.Format(CultureInfo.InvariantCulture, Strings.LoadFailed, id, (e as TargetInvocationException)?.InnerException?.Message ?? e.Message));
            return false;
        }
    }

    /// <summary>
    /// A mod's own assemblies, from its folder; the game's own (OpenPax.*
    /// and the runtime) come from the game, so the mod and the game share
    /// their types.
    /// </summary>
    // Collectible, so a mod set can be let go and another loaded in its place.
    // Assemblies are read from bytes, so their files are not held open and a
    // newer copy can be written over them.
    private sealed class ModLoadContext(string assemblyPath) : AssemblyLoadContext(Path.GetFileNameWithoutExtension(assemblyPath), isCollectible: true)
    {
        private readonly AssemblyDependencyResolver _resolver = new(Path.GetFullPath(assemblyPath));

        public Assembly Load(string path)
        {
            using var stream = new MemoryStream(File.ReadAllBytes(path));
            return LoadFromStream(stream);
        }

        protected override Assembly? Load(AssemblyName name)
        {
            // The game's and the runtime's own: from the game, whether loaded yet or not.
            if (name.Name is { } n && (n.StartsWith("OpenPax.", StringComparison.Ordinal) || n.StartsWith("System", StringComparison.Ordinal)
                || n.StartsWith("Microsoft.", StringComparison.Ordinal) || n is "netstandard" or "mscorlib"))
            {
                return null;
            }

            return _resolver.ResolveAssemblyToPath(name) is { } path ? Load(path) : null;
        }
    }
}
