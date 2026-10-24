# Rules for mod code

A C# mod runs inside the game's own rules, so it must play by the same
rules the game's code does. Most of this page comes down to one thing:
every copy of a game, given the same start and the same orders, must come
out the same.

## Deterministic, always

Network games keep every machine's game the same by running the same
steps, and saved replays check a hash of the whole game after every step.
A mod that does something differently on one machine splits the game.

- **No `System.Random`.** Use the mod's own stream, `context.Random(game)`
  (`ModRandom`). It is seeded from the game's seed and the mod's id, and
  its state is saved with the game. Do not draw from `Game.Random`: the
  game's own rules draw from it in a fixed order, and an extra draw
  changes everything after it.
- **No clock.** Not `DateTime.Now`, not timers, not the frame rate. Count
  in the game's steps (`Game.Tick`).
- **No order that is not fixed.** Iterating a `Dictionary` or `HashSet`
  where the order changes the result gives different results on different
  machines. Sort by an id first (`OrderBy(f => f.ObjectId)`).
- **No floating point surprises.** Plain `double` arithmetic is the same
  on every machine the game runs on; avoid `Math.FusedMultiplyAdd` and
  hardware specific intrinsics.

## State that lasts

A game is saved by its fields. A mod's own fields are not part of it, so
anything that must outlast a save, or reach a network client, goes in the
game:

- `context.Store(game)` (`ModStore`): the mod's values, under keys of its
  own (`id.key`), as text, integers, numbers or flags.
- `game.ModState` underneath it: string to string, saved, sent to clients
  and part of the state hash.

A mod's fields are fine for what it reads at load, its settings, and for
what it works out again from the game each time.

## Where a mod runs

- **Single player:** on the player's machine.
- **Network games:** the host's machine runs the game, and the mods' rules
  run there. Every player still needs the same mods, for the data packs
  and the screens, and the lobby sees to it.

What a mod tells a player (`Game.Tell`) reaches that player's own
machine in a network game.

## When things happen

| Hook | Runs |
|---|---|
| `ChangeTables` | when the mods load, on the game tables, before any game |
| `OnNewGame` | once, on a new game, after it is set up (not on a loaded one) |
| `OnStep` | after every strategic step, before the step's state hash |
| `OnLoadedGame` | once, on a saved game as it loads, with the mod's version it was saved with (null when it was not in it) |
| `OnEvent` and the typed event hooks | inside a step, at the moment the thing happens |
| `OnBattle` | when a fleet attacks; true takes the battle over |
| `OnColonyRating`, `OnAttackOdds` | when a screen asks; they must not change the game |

An event handler sees the game mid step: a ship destroyed is already gone
from its fleet, a colony captured already belongs to its new owner. It
may change the game, but carefully: removing ships or colonies while the
game is walking its lists for that step is best left to `OnStep`.

The rating and odds hooks are called while drawing, as often as every
frame. They must only read the game.

## What a mod may reach

Everything public in the game's assemblies, in the [reference](api.md).
The members made for mods (`[ModApi]`, listed first on `Game`) are the
ones meant for it: they keep the game's own bookkeeping straight (a ship
destroyed through `DamageShip` leaves its fleet, tells its owner and
raises the event). Other public members exist for the game's screens and
tools; reading them is fine, changing state through them is at your own
risk.

## Loading and letting go

Mods load when the game starts, and again in place whenever the set
changes ([multiplayer](multiplayer.md#loading-in-place)): `Load` runs
again on a fresh instance, and the old one's assembly is let go. Keep
nothing in static fields that outlives `Load`, and hold no game after the
step that gave it to you.

## Speed

`OnStep` runs every step, hundreds of times a minute at top speed, for
every star and fleet a mod walks. Keep the work small, or do it every few
steps (`game.Tick % 5 == 0`), as Simple Combat does.
