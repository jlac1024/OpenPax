# The manifest and load order

## mod.toml

Every mod has a `mod.toml` at the top of its folder:

```toml
id = "my-mod"            # letters, digits, '-', '_' and '.'
name = "My Mod"          # as Options shows it
version = "1.0"
description = "One line for the Options list."
assembly = "MyMod.dll"   # optional: a C# mod's assembly, in the mod's folder
# script = "main.lua"    # or instead: a Lua mod's script (lua.md)
```

| Field | Needed | Meaning |
|---|---|---|
| `id` | yes | The mod's name to the game: in the config file, in saved games, as the prefix of its saved values. Letters, digits, `-`, `_` and `.` only. |
| `name` | no | What Options shows; the id when left out. |
| `version` | no | Shown in Options, and part of the mod's key. |
| `description` | no | One line for Options. |
| `assembly` | no | The C# assembly to load. A mod without one (or a script) is a data pack only. |
| `script` | no | The Lua script to run instead (docs/modding/lua.md). A mod names an assembly or a script, not both. |

A game records the mods it started with (`Game.Mods`, as `id@version`),
and they are part of its state hash. In a network game a mod is named by
its id, version and a hash of its files (`id@version#hash`), and every
player gets the room host's set ([multiplayer](multiplayer.md)).

## Where mods are found

1. `mods/` in the user folder, the per-user folder the game keeps its
   config and saves in (on Linux `~/.local/share/OpenPax/OpenPax/`).
2. `mods/` next to the game, where the samples ship.

A mod in the user folder hides one with the same id next to the game, so
your own copy of a shipped mod (with changed settings, say) is the one
used.

## Turning mods on and ordering them

Options, on the MODS tab, lists every mod found. Each can be turned on or
off and moved up or down. The list is kept in the config file:

```toml
[mods]
enabled = ["simple-combat", "bounty"]
```

Changes load when you leave Options, in place, with no restart. With a
game running they wait for the next game: a running game keeps the mods it
started with.

Order matters in two places:

- Data packs: a file in a mod lower in the list wins over the same file
  in one higher up ([data packs](data-packs.md)).
- C# mods: they load in order, so their table changes apply in order, and
  where only one mod can answer (taking a battle over, rating a colony,
  the odds of an attack) the first one that answers has it.

## The dedicated server

The headless server, which hosts a room on a lobby, takes the mods to run
on the command line, by id and in order:

```bash
openpax --dedicated lobby.example.com --mods simple-combat,bounty
```

## When a mod does not load

A mod is left out, and the game says why when it starts, when:

- its `mod.toml` does not read, or has no id, or one already taken;
- it is turned on but not found;
- its assembly does not load or has no public class implementing `IMod`;
- its script is missing, or fails while it runs at load;
- its `Load` throws.

The other mods still load.
