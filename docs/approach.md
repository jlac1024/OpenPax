# Approach

How the port is built, in what order, and why. Written at the end of
Phase 1 from static study of the GOG executable (retail 1.072 plus three
GOG patches, see `gog-build.md`). Decisions Jeff made along the way are in
`decisions.md`; this file builds on them.

## What the original is

- One Win32 executable, Visual C++ 5.0, about 3,100 game functions in
  about 66 source modules (linked in alphabetical order, which is how
  modules are told apart), plus the C/C++ runtime.
- 2D only: DirectDraw, 640x480 at 8 bits per pixel (`SetDisplayMode(640,
  480, 8)`), 16 palettes in `Data/PalData`.
- Sound through Miles 3.6B: PCM WAV samples, CD audio for music (Ogg
  files in the GOG build), Smacker movies with Miles sound.
- Data in a Mac-style resource container (`formats/resources.md`).
- One global LCG for all randomness; x87 floating point at 53-bit
  precision; `fsin`/`fcos` in universe code (`numerics.md`).
- Multiplayer over TCP/UDP, Mplayer IPX and modem/serial. Only studied to
  inform our own protocol, since there is no legacy netplay.

## Order of work

Phase 2 (data and media) in this order, each with tests and a dump:

1. Resource container reader (`OpenPax.Data`), with the hash check of
   the GOG files from the catalog.
2. Palettes, then `PPIC` images. The image decoder is taken from the
   code that reads `PPIC` resources. Dumps go to `build/dump/` as PNG
   for Jeff to check against the original's art.
3. `STR#` strings, then the game tables (`RACE`, `TECH`, `HULL`, `SHIP`,
   `VIEW`), fonts (`pft*`) and `PrefData` (defaults only).
4. WAV reader and our own Vorbis decoder for music.
5. Video: ffmpeg in `deps/` converts `_Movie/*.smk` to MP4 in the import
   step; the libavcodec decode-only build and our bindings play them.
   Encode settings are chosen by comparing frames with the `.smk`.

Phase 3 (simulation, headless), in dependency order: RNG and numerics
helpers, game setup, universe generation (the first fidelity target: a
seed must give the original's galaxy), planets and orbits, empires and
colonies, economy, research, ship design, fleets and movement, combat,
espionage, diplomacy, then AI. Each subsystem is ported module by module.

## Reproducing the original's numbers

- **RNG:** the port's `Random` reproduces the LCG exactly, including the
  `(state >> 16) & 0x7FFF` output and unsigned modulo. There is one stream
  per game, owned by the game state (no static state). Every draw the
  original makes, the port makes in the same order, including discarded
  ones.
- **Integers:** C semantics are kept where they matter: 32-bit wraparound,
  truncating division, `_ftol` truncation toward zero for float to int,
  and signed versus unsigned compares as the original had them.
- **Floating point:** the simulation uses `double`. Since the original
  runs at 53-bit precision with round to nearest, `+ - * /` and `sqrt`
  match IEEE double except where values would overflow or underflow the
  double range, which game values do not approach. Where the original
  stores to a 32-bit `float`, the port does too, to get the same rounding.
- **Trig:** our own `sin`/`cos` in `OpenPax.Sim` that returns what
  x87 `fsin`/`fcos` return at 53-bit precision over the argument ranges
  the game uses (decisions.md). Reference values come from a small
  native harness that runs those instructions on the build machine.
  `atan2` (`fpatan`, AI only) is handled the same way.
- **Checking fidelity without the original running:** galaxy generation
  is checked against values worked out from the code and, where Jeff can
  provide them, screenshots or saves from the original on a Windows
  machine.

## AI and determinism

The original simulation is already tick based. A game clock runs whole
steps paced by real time (the game speed setting picks 600 down to 13 ms
per step; combat uses 40 ms), and the AI is a scheduled task every 20, 10 or 5
ticks by difficulty, with no time budget (`numerics.md`, `sim/time.md`). So the original's AI is
deterministic for a seed and inputs, and the port can follow it closely
instead of replacing a time budget. The port runs the simulation as
`Step()` calls on a headless game state. The host decides when to call
`Step()`; nothing inside it reads the clock.

The galaxy seed is part of the new-game settings. The port makes the
same LCG draws the original does, including cosmetic ones such as the
wormhole animation frame.

## Rendering path

**SDL_Renderer, not the SDL GPU API.** The original composes every frame
into one 640x480 8-bit surface with palette changes. The port does the
same in a managed 8-bit framebuffer, converts it through the current
palette into a streaming RGBA texture once per frame, and lets
SDL_Renderer scale it to the window. Movies decode to YUV frames that
SDL_Renderer can show directly as IYUV/NV12 textures. Nothing the game
draws needs shaders, and SDL_Renderer runs on every target without
Vulkan. The GPU API stays an option for Phase 6 scaling work if it is
ever needed.

## Codecs

- Sound effects: PCM WAV, our own reader.
- Music: Ogg Vorbis (GOG's CD rip), our own decoder.
- Movies: converted to MP4/H.264 at import, decoded by libavcodec
  (decisions.md).

## GOG changes

All three GOG code patches deal with the missing CD (disc check and the
music gate) and none touch game rules. The port has no disc check and
always plays music from files, so it behaves like the GOG build. Nothing
needs a toggle.

## Original multiplayer

The original is deterministic lockstep in a star: every machine runs the
full simulation from the host's seed, and only orders are exchanged. Its
only desync check is one RNG sample per packet, and a mismatch drops the
player (`netcode/original.md`). The modern protocol does not copy that:
Jeff chose a server authoritative model (`decisions.md`). Only the server
runs the simulation; clients send orders and apply the state it sends,
checked against a per-tick state hash, with a full snapshot to recover.
Hosting and the update form are settled at the start of Phase 5.

Two rules still hold for Phase 3, for the server, replays and tests: every player
action enters the simulation as an order applied at a tick boundary, and
nothing outside the simulation (UI, sound, effects) draws from the
simulation RNG. Where the original's UI does draw from it (the galaxy
preview), the port keeps those draws in the same place for fidelity but
behind the simulation's own API.

## Remaining deferred items

| Item | Status |
|---|---|
| Test framework | xUnit v3 (decisions.md) |
| Audio and video codecs | Settled above |
| GOG versus retail behavior | Same for gameplay; nothing to choose |
| Rendering path | SDL_Renderer (above) |
| Legacy netplay scope | No legacy netplay (decisions.md) |
| Loading original save files | Phase 4 (moved at the engine sign-off) |
| Modern sync model | Server authoritative (decisions.md); hosting and update form at the start of Phase 5 |
| Lobby hosting, NAT, accounts | Start of Phase 5 |
| Scaling, widescreen, UI scaling | Phase 6 |
| Gameplay fix toggles | Phase 6 |
| Mod format and API | Start of Phase 7 |
| Final name | Before release |
