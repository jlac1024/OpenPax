using OpenPax.Lua.Runtime;
using OpenPax.Sim;
using OpenPax.Sim.Galaxy;
using OpenPax.Sim.Players;

namespace OpenPax.Modding.Lua;

/// <summary>
/// The game's objects as a script sees them (docs/modding/lua.md): read-only
/// views of players, ships, planets and techs, and the game itself with
/// the few things a mod may do to it. Each kind has one metatable, made
/// once per script.
/// </summary>
internal sealed class LuaViews
{
    private readonly LuaState _state;
    private readonly ModContext _context;
    private readonly LuaTable _gameMeta;
    private readonly LuaTable _playerMeta;
    private readonly LuaTable _shipMeta;
    private readonly LuaTable _planetMeta;
    private readonly LuaTable _techMeta;
    private readonly LuaTable _storeMeta;
    private readonly LuaValue _tell;
    private readonly LuaValue _giveMoney;
    private readonly LuaValue _playerByIndex;

    public LuaViews(LuaState state, ModContext context)
    {
        _state = state;
        _context = context;
        _playerMeta = Meta("player", (o, key) => o is Player p ? key switch
        {
            "index" => (long)p.Index,
            "name" => p.Name,
            "money" => p.Money,
            "human" => !p.IsAi,
            "eliminated" => p.Eliminated,
            _ => LuaValue.Nil,
        } : LuaValue.Nil);
        _shipMeta = Meta("ship", (o, key) => o is Design d ? key switch
        {
            "name" => d.Name,
            "type" => (long)d.Type,
            "structure" => (long)d.Structure,
            "max_structure" => (long)d.MaxStructure,
            _ => LuaValue.Nil,
        } : LuaValue.Nil);
        _planetMeta = Meta("planet", (o, key) => o is Planet p ? key switch
        {
            "name" => p.Name,
            "class" => (long)p.Class,
            _ => LuaValue.Nil,
        } : LuaValue.Nil);
        _techMeta = Meta("tech", (o, key) => o is TechInfo t ? key switch
        {
            "id" => (long)t.Id,
            "kind" => (long)t.Kind,
            _ => LuaValue.Nil,
        } : LuaValue.Nil);

        _tell = LuaState.Function("tell", (_, a) =>
        {
            var game = GameOf(a, 0, "tell");
            game.Tell(PlayerOf(a, 1, "tell"), a.Length > 2 ? _state.ToDisplay(a[2]) : "");
            return [];
        });
        _giveMoney = LuaState.Function("give_money", (_, a) =>
        {
            GameOf(a, 0, "give_money");
            var amount = Numbers(a, 2, "give_money");
            OpenPax.Sim.Game.GiveMoney(PlayerOf(a, 1, "give_money"), amount);
            return [];
        });
        _playerByIndex = LuaState.Function("player", (_, a) =>
        {
            var game = GameOf(a, 0, "player");
            var index = a.Length > 1 && a[1].IsInteger ? a[1].AsInteger : -1;
            return [index >= 0 && index < game.Players.Count ? Player(game.Players[(int)index]) : LuaValue.Nil];
        });
        _storeMeta = StoreMeta();
        _gameMeta = Meta("game", (o, key) => o is Game g ? key switch
        {
            "tick" => (long)g.Tick,
            "players" => Players(g),
            "store" => LuaValue.Of(new LuaUserData(new StoreHandle(g), _storeMeta)),
            "tell" => _tell,
            "give_money" => _giveMoney,
            "player" => _playerByIndex,
            _ => LuaValue.Nil,
        } : LuaValue.Nil);
    }

    public LuaValue Game(Game game) => LuaValue.Of(new LuaUserData(game, _gameMeta));

    public LuaValue Player(Player? player) => player is null ? LuaValue.Nil : LuaValue.Of(new LuaUserData(player, _playerMeta));

    public LuaValue Ship(Design? ship) => ship is null ? LuaValue.Nil : LuaValue.Of(new LuaUserData(ship, _shipMeta));

    public LuaValue Planet(Planet? planet) => planet is null ? LuaValue.Nil : LuaValue.Of(new LuaUserData(planet, _planetMeta));

    public LuaValue Tech(TechInfo? tech) => tech is null ? LuaValue.Nil : LuaValue.Of(new LuaUserData(tech, _techMeta));

    private LuaValue Players(Game game)
    {
        var list = new LuaTable();
        for (var i = 0; i < game.Players.Count; i++)
        {
            list.Set(i + 1L, Player(game.Players[i]));
        }

        return list;
    }

    // A view's metatable: its fields through __index, its kind as __name, and no writing.
    private static LuaTable Meta(string kind, Func<object, string, LuaValue> field)
    {
        var meta = new LuaTable();
        meta.Set("__name", kind);
        meta.Set("__index", LuaState.Function(kind, (_, a) =>
            [a.Length > 1 && a[0].AsUserData is { } u && a[1].IsString ? field(u.Host, a[1].AsString) : LuaValue.Nil]));
        meta.Set("__newindex", LuaState.Function(kind, (_, a) =>
            throw new LuaException($"a {kind} cannot be changed from a script (docs/modding/lua.md)")));
        return meta;
    }

    /// <summary>
    /// game.store: the mod's saved values. Numbers, booleans and text go in as
    /// text (as ModStore keeps them) and come back as what they read as.
    /// </summary>
    private LuaTable StoreMeta()
    {
        // The functions take the key first; called as store:get(key), the store itself comes first and is skipped.
        static int First(LuaValue[] a) => a.Length > 0 && a[0].AsUserData?.Host is StoreHandle ? 1 : 0;
        StoreHandle Handle(LuaValue[] a) => (a.Length > 0 ? a[0].AsUserData?.Host as StoreHandle : null) ?? throw new LuaException("call the store's functions with ':' (game.store:get(key))");

        var get = LuaState.Function("get", (_, a) =>
        {
            var handle = Handle(a);
            var key = Key(a, First(a), "get");
            return [Read(_context.Store(handle.Game).Get(key))];
        });
        var set = LuaState.Function("set", (_, a) =>
        {
            var handle = Handle(a);
            var at = First(a);
            var key = Key(a, at, "set");
            var value = at + 1 < a.Length ? a[at + 1] : LuaValue.Nil;
            _context.Store(handle.Game).Set(key, value.IsNil ? null : value.Type switch
            {
                LuaType.Boolean => value.IsTruthy ? "true" : "false",
                LuaType.Number or LuaType.String => value.IsFloat ? value.AsFloat.ToString("R", System.Globalization.CultureInfo.InvariantCulture) : value.ToString(),
                _ => throw new LuaException($"the store keeps text, numbers and booleans, not a {LuaState.TypeName(value)}"),
            });
            return [];
        });
        var remove = LuaState.Function("remove", (_, a) =>
        {
            var handle = Handle(a);
            _context.Store(handle.Game).Remove(Key(a, First(a), "remove"));
            return [];
        });
        return Meta("store", (_, key) => key switch
        {
            "get" => get,
            "set" => set,
            "remove" => remove,
            _ => LuaValue.Nil,
        });
    }

    private static string Key(LuaValue[] a, int at, string function) =>
        at < a.Length && (a[at].IsString || a[at].IsNumber) ? a[at].ToString() : throw new LuaException($"bad argument to '{function}' (key expected)");

    // A stored text back as a value: true/false, a number when it reads as one, else the text.
    private static LuaValue Read(string? text) => text switch
    {
        null => LuaValue.Nil,
        "true" => LuaValue.True,
        "false" => LuaValue.False,
        _ => LuaValue.ParseNumber(text) is { IsNil: false } n ? n : LuaValue.Of(text),
    };

    private static Game GameOf(LuaValue[] a, int i, string function) =>
        (i < a.Length ? a[i].AsUserData?.Host as Game : null) ?? throw new LuaException($"call '{function}' on the game with ':' (game:{function}(...))");

    private static Player PlayerOf(LuaValue[] a, int i, string function) =>
        (i < a.Length ? a[i].AsUserData?.Host as Player : null) ?? throw new LuaException($"bad argument #{i} to '{function}' (player expected)");

    private static double Numbers(LuaValue[] a, int i, string function) =>
        i < a.Length && a[i].IsNumber ? a[i].AsFloat : throw new LuaException($"bad argument #{i} to '{function}' (number expected)");

    // The store's view holds the game it belongs to.
    private sealed record StoreHandle(Game Game);
}
