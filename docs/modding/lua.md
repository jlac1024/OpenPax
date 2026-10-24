# Lua scripting (design)

Status: first cut. The interpreter (`src/OpenPax.Lua`), the event, step
and tick hooks, file reading, and save handling across mod changes are
built and tested; the battle and rating hooks are planned (see the end). Bounty ships in both forms,
`samples/Bounty` (C#) and `samples/BountyLua` (Lua), and does the same
in each.

## Why

C# mods need .NET and a build. A Lua mod is a folder of text files: write
`main.lua` in any editor, turn the mod on, start a game. Lua is the
language most game players who mod have met, small enough to learn in an
afternoon, and easy to sandbox.

## The shape

A Lua mod is an ordinary mod (manifest, optional data pack) whose
`mod.toml` names a script instead of an assembly:

```toml
id = "bounty-lua"
name = "Bounty (Lua)"
version = "1.0"
script = "main.lua"
```

The script runs once when the mods load, and says what it changes by
calling the `mod` table's hooks, as a C# mod does from `IMod.Load`. It
uses the same hooks underneath (`ModContext`), so a Lua mod has the same
place in the game as a C# one: the same load order, the same mod set
check in network games, the same saved state.

## The interpreter

Our own, in `src/OpenPax.Lua`, written for the game (no NuGet package, no
native library). It reads Lua 5.4 source and runs it by turning each
function into C# closures once, when the script loads.

The language as Lua 5.4 has it, except:

- no `goto` and labels, no coroutines, no `<const>` and `<close>`;
- integers and floats as in 5.4 (64-bit integers, `//`, the bitwise
  operators, integer for loops);
- metatables with `__index`, `__newindex`, `__call`, the arithmetic and
  comparison events, `__concat`, `__len`, `__unm`, `__tostring` and
  `__eq`; no weak tables, no finalizers.

## Safe and the same everywhere

A mod's script runs in every network game on the host, and in every
replay; it must come out the same on every machine (rules.md). So the
interpreter leaves out or replaces whatever could differ or reach outside
the game:

- **No `os`, `io`, `debug`, `load`, `loadstring`, `dofile`.** No clock, no
  files outside the mod's folder, no code from text.
- **`math.random` is the mod's own stream** (`ModRandom`): seeded from the
  game and the mod's id, saved with the game. `math.randomseed` does
  nothing. Outside a game (while loading) it raises an error.
- **`pairs` goes in a fixed order** on every machine (Lua leaves it open):
  the keys 1 to n of a list first, then the others in the order they were
  first added. A TOML file read with `mod.read_toml` comes in sorted by key.
- **Numbers** follow the game's rule for doubles (rules.md). `math.sin`,
  `cos`, `tan`, `exp`, `log` and powers with a fraction are left out for
  now, as the C library's results can differ between Windows, Linux and
  macOS; whole-number powers are worked out exactly.
- **A step budget.** Each call from the game into the script may run so
  many steps (about ten million); past that it stops with an error, so a
  mistaken endless loop cannot hang the game. The count is the same on
  every machine, so the error is too.
- **Errors stay in the mod.** An error in a hook is logged with the
  script's file and line, and that call is skipped; the game goes on.

## The API

The script sees the standard library above, plus:

### `mod`

| Member | What it does |
|---|---|
| `mod.id`, `mod.version` | from mod.toml |
| `mod.log(text)` | a line for the player when the game starts, and the console |
| `mod.read_text(file)` | a text file of the mod's folder, or nil |
| `mod.read_toml(file)` | a TOML file of the mod's folder as a table (empty when missing) |
| `require(name)` | runs `name.lua` from the mod's folder once and gives back what it returns |
| `mod.on_new_game(f)` | `f(game)` once on a new game, before its first step |
| `mod.on_load(f)` | `f(game, saved_version)` when a saved game is loaded; see Saves |
| `mod.on_step(f)` | `f(game)` after every step |
| `mod.every(ticks, f)` | `f(game)` after every step whose tick is a multiple of `ticks` |
| `mod.on_tech_learned(f)` | `f(game, player, tech)` |
| `mod.on_ship_built(f)` | `f(game, ship, planet)` |
| `mod.on_ship_destroyed(f)` | `f(game, ship, owner, by)`; `by` is nil when no one did it |
| `mod.on_colony_founded(f)` | `f(game, player, planet)` |
| `mod.on_colony_changed_hands(f)` | `f(game, new_owner, old_owner, planet)` |
| `mod.on_colony_destroyed(f)` | `f(game, player, planet)` |
| `mod.on_treaty_changed(f)` | `f(game, from, toward, slot, value)` |
| `mod.on_player_eliminated(f)` | `f(game, player)` |

Files are named relative to the mod's folder, with `/` between folders.
A name that leaves the folder (`..`, an absolute path) is refused.
Pictures and sounds belong in the mod's data pack (data-packs.md), which
the game loads itself.

### `game`

| Member | What it is |
|---|---|
| `game.tick` | the game's step count |
| `game.players` | the players, a list from 1 |
| `game:player(index)` | a player by the game's index (from 0) |
| `game.store` | the mod's saved values: `game.store:get(key)`, `game.store:set(key, value)` with text, numbers or booleans (nil removes), `game.store:remove(key)`; a value comes back as what it reads as (`"7"` as 7, `"true"` as true) |
| `game:tell(player, text)` | a message in that player's history |
| `game:give_money(player, credits)` | adds (or with a negative amount, takes) credits |

### Players, ships, planets, techs

Read-only views of the game's objects: a player's `index`, `name`,
`money`, `human`, `eliminated`; a ship's `name`, `type` (hull type),
`structure`, `max_structure`; a planet's `name` and `class`; a tech's
`id` and `kind`. Two views of the same object are equal (`==`). Changing
the game goes through the hooks and `game`'s functions, so the API can
grow without letting a script break the game's own bookkeeping.

## Saves across mod changes

A game records the mods it plays with, id and version, and each mod keeps
its state in the game's mod state under its own id, which is saved.
Loading a save then goes:

- **A mod the save has, the same version:** it carries on.
- **A newer (or older) version of a mod:** its `on_load` hook (C#:
  `ModContext.OnLoadedGame`) gets the version the game was saved with,
  so it can bring its saved values up to date; then the game records the
  new version. A mod with no hook simply carries on with its values.
- **A mod the save has that is not turned on (or not installed):** the
  save does not load. The load lists mark it with an orange warning whose
  tooltip names the mods, and LOAD stays off for it until they are turned
  on. A game is not played without the mods it was made with, so their
  saved values are never left behind.
- **A mod turned on that the save does not have:** it joins the game;
  its `on_load` gets nil as the version, its cue to set itself up.

A mod's script itself is not saved: it runs again when the mods load, and
anything a hook needs later must be in `game.store`, not in a script's
variables (as rules.md says for C# fields).

One limit stays: a mod that changes the game tables (new techs, hulls,
as Arsenal does) is part of what a save is made of, and a save made with
it may not load without it. Naming the missing mod when that happens is
planned.

## A whole mod

`samples/BountyLua` is Bounty (`samples/Bounty`) in Lua, and ships with
the game as "Bounty (Lua)":

```lua
local settings = mod.read_toml("settings.toml")
local per_hull = settings.credits_per_hull or 50
local double_chance = settings.double_chance or 0.1

mod.on_ship_destroyed(function(game, ship, owner, by)
  if by == nil or by == owner then
    return
  end

  local bonus = math.random() < double_chance
  local credits = per_hull * (ship.type + 1) * (bonus and 2 or 1)
  game:give_money(by, credits)

  local key = "kills." .. by.index
  local kills = (game.store:get(key) or 0) + 1
  game.store:set(key, kills)
  game:tell(by, string.format("Bounty: %s credits (%d kills)", credits, kills))
end)
```

Turn it on in Options, MODS, and start a game.

## Planned next

- `mod.on_battle`, `mod.on_colony_rating`, `mod.on_attack_odds`.
- Table changes (techs, hulls) from Lua.
- Coroutines, for scripts that wait over several steps.
- Deterministic `math.sin` and friends, from the game's own tables.
