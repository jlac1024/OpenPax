# OpenPax

An open-source engine for *Pax Imperia: Eminent Domain* (1997), the
real-time 4X space strategy game. Play your own GOG copy natively on
Windows, macOS and Linux, with widescreen, online multiplayer and mods.

> **Status: in development, not yet released.** It plays today when built
> from source on Linux with the game's files in place (below). Release
> builds for all three systems, which import from your GOG copy on first
> launch, are on the way.

## What it is

OpenPax plays the game as it was designed, with the same rules, the same
galaxies from the same seeds, and the same feel, on modern systems with no
emulator or compatibility layer.

OpenPax ships no game content. It reads the art, sounds, movies and data
from your own copy of the game, which you need to own (it is sold on
[GOG](https://www.gog.com)).

On top of the original game it adds:

- native builds for Windows, macOS (Apple Silicon) and Linux;
- widescreen and high-resolution displays with sharp, upscaled art;
- online and LAN multiplayer through a lobby, with chat and a ready check;
- optional fixes for the original's bugs, each off by default;
- mods: art and data packs, new technologies, ship designs and planet
  types, and rule changes in C#, shared automatically with the other
  players in a room ([modding guide](docs/modding.md)).

## Building from source (Linux)

You need:

- the [.NET 10 SDK](https://dotnet.microsoft.com/download);
- a C compiler, `make`, CMake, NASM, Python 3, `rsync` and `zip`;
- your installed GOG copy of *Pax Imperia: Eminent Domain*.

Then:

1. Copy the installed game folder to
   `references/gog/Pax Imperia - Eminent Domain/` in this repository. The
   importer checks the files against the GOG release and refuses anything
   else.
2. Build. The first build downloads and builds SDL 3 and FFmpeg from
   pinned sources and imports the game's data, which takes a while; later
   builds only redo what changed.

   ```bash
   ./build.sh
   ```

3. Play:

   ```bash
   ./run.sh
   ```

`./build.sh --dist` also makes Windows and macOS builds
([platforms](docs/platforms.md)). Tests that need the game's files skip
themselves when it is missing; the rest run without it.

## Repository

| Folder | What |
|---|---|
| `src/` | the game: `OpenPax.Sim` (rules, no graphics), `OpenPax.Ai`, `OpenPax.Data` (reading the game's files), `OpenPax.Ui`, `OpenPax.Net`, `OpenPax.Modding`, and the rest |
| `tests/` | a test project for each |
| `samples/` | example mods |
| `docs/` | file formats, the rules as studied, netcode, modding, platforms |
| `tools/` | build and import helpers |

The simulation is deterministic: the same seed and the same orders give
the same game, tick for tick, which saves, replays and multiplayer rely on.
`docs/numerics.md` explains how that holds across systems.

## License

OpenPax is free software under the [GNU General Public License,
version 3](LICENSE). The libraries and tools it builds on are listed in
[THIRD-PARTY-NOTICES.md](THIRD-PARTY-NOTICES.md).

## Disclaimer

OpenPax is a fan project. *Pax Imperia: Eminent Domain* was developed by
Heliotrope Studios, and the game and the *Pax Imperia* name belong to THQ
Nordic GmbH. OpenPax is not affiliated with or endorsed by Heliotrope
Studios, THQ Nordic or GOG. No part of the original game is included in
this repository or in OpenPax's builds.
