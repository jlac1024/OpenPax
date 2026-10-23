<p align="center"><img src="src/OpenPax.Host/Icon/openpax-256.png" width="128" height="128" alt="OpenPax"></p>

# OpenPax

An open-source engine for *Pax Imperia: Eminent Domain* (1997), the
real-time 4X space strategy game. Play your own GOG copy natively on
Windows, macOS and Linux, with widescreen, online multiplayer and mods.

> **Status: early releases.** Pre-release builds for Windows (installer or
> zip), macOS (Apple Silicon) and Linux are on the
> [releases page](https://github.com/jlac1024/OpenPax/releases). On first
> start they import the game from your own GOG copy, and they keep
> themselves up to date. Expect rough edges, and please report what you find.

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
- a C compiler, `make`, CMake, NASM, Python 3 and `rsync`;
- your own copy of *Pax Imperia: Eminent Domain* from GOG, to play (not
  to build).

Then:

1. Build. The first build downloads and builds SDL 3 and FFmpeg from
   pinned sources, which takes a while; later builds only redo what
   changed.

   ```bash
   ./build.sh
   ```

2. Play:

   ```bash
   ./run.sh
   ```

   On first start the game asks for your copy of the game, as a release
   does: an installed GOG copy, or GOG's offline backup installer
   (`setup_pax_imperia_2.0.0.12.exe`). It imports it once and starts.

Only the C# code? `dotnet build OpenPax.slnx` and `dotnet test` need just
the .NET SDK; the game itself needs the native libraries `build.sh` makes.
Tests that need the game's files skip themselves without them. Release
bundles for each system are made by `tools/package.sh`, and by GitHub
Actions for every release ([platforms](docs/platforms.md)).

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
