using System.Globalization;
using OpenPax.Lua.Runtime;
using OpenPax.Sim;

namespace OpenPax.Modding.Lua;

/// <summary>
/// A Lua mod (docs/modding/lua.md): its script run once when the mods load,
/// in a world of its own, saying through the mod table what it hooks into.
/// Each hook runs with a fresh step budget and the game's mod random
/// stream; an error in one is told to the players once and that call is
/// skipped, and the game goes on.
/// </summary>
internal sealed class LuaModScript
{
    private readonly ModInfo _mod;
    private readonly ModContext _context;
    private readonly LuaState _state = new();
    private readonly LuaViews _views;
    private readonly LuaTable _loaded = new();
    private readonly HashSet<string> _told = [];
    private Game? _game;

    private LuaModScript(ModInfo mod, ModContext context)
    {
        _mod = mod;
        _context = context;
        _views = new LuaViews(_state, context);
        _state.Output = context.Log;
        _state.Random = new GameRandom(this);
        _state.Globals.Set("mod", ModTable());
        _state.Globals.Set("require", LuaState.Function("require", Require));
    }

    /// <summary>Runs a mod's script into its context; false, with the reason in errors, when it fails.</summary>
    public static bool Load(ModInfo mod, string scriptFile, ModContext context, List<string> errors)
    {
        var id = mod.Manifest.Id;
        if (ModFiles.Resolve(mod.Folder, scriptFile) is not { } path || !System.IO.File.Exists(path))
        {
            errors.Add(string.Format(CultureInfo.InvariantCulture, Strings.ScriptMissing, id, scriptFile));
            return false;
        }

        try
        {
            var script = new LuaModScript(mod, context);
            script._state.Run(script._state.Load(System.IO.File.ReadAllText(path), scriptFile));
            return true;
        }
        catch (Exception e) when (e is LuaException or LuaBudgetException or IOException or UnauthorizedAccessException)
        {
            errors.Add(string.Format(CultureInfo.InvariantCulture, Strings.ScriptFailed, id, e.Message));
            return false;
        }
    }

    private LuaTable ModTable()
    {
        var m = new LuaTable();
        m.Set("id", _mod.Manifest.Id);
        m.Set("version", _mod.Manifest.Version);
        Add(m, "log", a =>
        {
            _context.Log(string.Join(' ', a.Select(_state.ToDisplay)));
            return [];
        });
        Add(m, "read_text", a =>
        {
            var path = ModFile(a, "read_text");
            return [path is not null && System.IO.File.Exists(path) ? LuaValue.Of(System.IO.File.ReadAllText(path)) : LuaValue.Nil];
        });
        Add(m, "read_toml", a =>
        {
            var name = Name(a, "read_toml");
            if (ModFiles.Resolve(_mod.Folder, name) is null)
            {
                throw new LuaException($"'{name}' is outside the mod's folder");
            }

            return [LuaToml.ToLua(_context.ReadToml(name))];
        });

        Add(m, "on_new_game", a => Hook(a, "on_new_game", f => _context.OnNewGame(g => Call(g, f, _views.Game(g)))));
        Add(m, "on_step", a => Hook(a, "on_step", f => _context.OnStep(g => Call(g, f, _views.Game(g)))));
        Add(m, "on_load", a => Hook(a, "on_load", f => _context.OnLoadedGame((g, saved) => Call(g, f, _views.Game(g), LuaValue.Of(saved)))));
        Add(m, "every", a =>
        {
            var ticks = a.Length > 0 && a[0].IsInteger && a[0].AsInteger > 0 ? a[0].AsInteger : throw new LuaException("bad argument #1 to 'every' (a whole number of ticks above 0 expected)");
            var f = a.Length > 1 ? a[1].AsFunction : null;
            if (f is null)
            {
                throw new LuaException("bad argument #2 to 'every' (function expected)");
            }

            _context.OnStep(g =>
            {
                if (g.Tick % ticks == 0)
                {
                    Call(g, f, _views.Game(g));
                }
            });
            return [];
        });
        Add(m, "on_tech_learned", a => Hook(a, "on_tech_learned", f => _context.OnTechLearned((g, p, t) => Call(g, f, _views.Game(g), _views.Player(p), _views.Tech(t)))));
        Add(m, "on_ship_built", a => Hook(a, "on_ship_built", f => _context.OnShipBuilt((g, s, p) => Call(g, f, _views.Game(g), _views.Ship(s), _views.Planet(p)))));
        Add(m, "on_ship_destroyed", a => Hook(a, "on_ship_destroyed", f => _context.OnShipDestroyed((g, s, owner, by) => Call(g, f, _views.Game(g), _views.Ship(s), _views.Player(owner), _views.Player(by)))));
        Add(m, "on_colony_founded", a => Hook(a, "on_colony_founded", f => _context.OnColonyFounded((g, p, planet) => Call(g, f, _views.Game(g), _views.Player(p), _views.Planet(planet)))));
        Add(m, "on_colony_changed_hands", a => Hook(a, "on_colony_changed_hands", f => _context.OnColonyChangedHands((g, to, from, planet) => Call(g, f, _views.Game(g), _views.Player(to), _views.Player(from), _views.Planet(planet)))));
        Add(m, "on_colony_destroyed", a => Hook(a, "on_colony_destroyed", f => _context.OnColonyDestroyed((g, p, planet) => Call(g, f, _views.Game(g), _views.Player(p), _views.Planet(planet)))));
        Add(m, "on_treaty_changed", a => Hook(a, "on_treaty_changed", f => _context.OnTreatyChanged((g, from, toward, slot, value) => Call(g, f, _views.Game(g), _views.Player(from), _views.Player(toward), LuaValue.Of((long)slot), LuaValue.Of((long)value)))));
        Add(m, "on_player_eliminated", a => Hook(a, "on_player_eliminated", f => _context.OnPlayerEliminated((g, p) => Call(g, f, _views.Game(g), _views.Player(p)))));
        return m;
    }

    private static void Add(LuaTable table, string name, Func<LuaValue[], LuaValue[]> body) => table.Set(name, LuaState.Function(name, (_, a) => body(a)));

    private static LuaValue[] Hook(LuaValue[] a, string name, Action<LuaFunction> register)
    {
        register(a.Length > 0 && a[0].AsFunction is { } f ? f : throw new LuaException($"bad argument #1 to '{name}' (function expected)"));
        return [];
    }

    /// <summary>
    /// A hook's call: the game at hand for math.random, a fresh budget, and
    /// an error told once to the players (and to the log) instead of
    /// stopping the game; the same everywhere, as the script is.
    /// </summary>
    private void Call(Game game, LuaFunction function, params LuaValue[] args)
    {
        var outer = _game;
        _game = game;
        try
        {
            _state.Run(function, args);
        }
        catch (Exception e) when (e is LuaException or LuaBudgetException)
        {
            var message = string.Format(CultureInfo.InvariantCulture, Strings.HookFailed, _mod.Manifest.Name, e.Message);
            if (_told.Add(message))
            {
                _context.Log(e.Message);
                foreach (var player in game.Players.Where(p => !p.IsAi))
                {
                    game.Tell(player, message);
                }
            }
        }
        finally
        {
            _game = outer;
        }
    }

    // require("name"): name.lua from the mod's folder, run once; later calls give what it returned.
    private LuaValue[] Require(LuaState state, LuaValue[] a)
    {
        var name = Name(a, "require");
        var known = _loaded.Get(name);
        if (!known.IsNil)
        {
            return [known];
        }

        var file = name.Replace('.', '/') + ".lua";
        if (ModFiles.Resolve(_mod.Folder, file) is not { } path || !System.IO.File.Exists(path))
        {
            throw new LuaException($"module '{name}' not found ({file} in the mod's folder)");
        }

        var result = LuaState.First(state.Call(LuaValue.Of(state.Load(System.IO.File.ReadAllText(path), file)), [LuaValue.Of(name)]));
        var value = result.IsNil ? LuaValue.True : result;
        _loaded.Set(name, value);
        return [value];
    }

    private static string Name(LuaValue[] a, string function) =>
        a.Length > 0 && a[0].IsString ? a[0].AsString : throw new LuaException($"bad argument #1 to '{function}' (string expected)");

    private string? ModFile(LuaValue[] a, string function)
    {
        var name = Name(a, function);
        return ModFiles.Resolve(_mod.Folder, name) ?? throw new LuaException($"'{name}' is outside the mod's folder");
    }

    // math.random in a hook: the mod's own stream in the game at hand.
    private sealed class GameRandom(LuaModScript script) : ILuaRandom
    {
        private ModRandom Stream => script._game is { } game ? script._context.Random(game) : throw new LuaException("math.random is only available while a game runs (docs/modding/lua.md)");

        public long Next(long max) => max <= int.MaxValue ? Stream.Next((int)max) : throw new LuaException("math.random's range is too large");

        public double NextDouble() => Stream.NextDouble();
    }
}
