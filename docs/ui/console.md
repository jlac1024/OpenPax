# Developer console (port only)

The original has no console. The port adds one so the developers' cheats
(see [developer-window.md](developer-window.md)) can be used without
patching a key into the command handler.

The backtick key opens and closes it; Escape closes it too. While it is
open it takes every key and every other command is ignored. It draws
over the top twelve lines of the screen.

Cheats start off each run. `cheats on` turns them on; a cheat typed
while they are off is refused. Without a game under way the cheat words
say so and do nothing.

| Command | What it does |
|---|---|
| `help` | Lists the commands. |
| `cheats on`, `cheats off` | Turns the cheats on or off. |
| `players` | Lists the players with their numbers. |
| `hash` | Shows the game's state hash. |
| `devwindow` | Opens the developers' cheat window (mode 21). |
| `money`, `buildall`, `kill`, `research`, `spies`, `ships`, `colonize`, `explore`, `planetcombat`, `fleetcombat`, `synctest` | Cheat kinds 0 to 10, for the local player or for the player number given after the word. |

A cheat changes the game state like any other order, so a game that has
used one no longer matches the original or a replay without it.
