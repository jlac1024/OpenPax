# Engine sign-off review

The hard gate at the end of Phase 3: Jeff reviews the data layer, the
decoders and the simulation. State as of 2026-10-18, commit after
b4cc753 on `dev`.

## Short version

- Everything Phase 3 lists is ported except one piece left for Phase 4 on
  purpose: battles with a human side, fought step by step. They resolve
  at once like computer battles for now.
- The simulation is deterministic: two runs of a seed give the same state
  hash, and a recorded replay reproduces every tick's hash (checked again
  for this review: 6 players, 40000 ticks).
- Computer player games end: 8 of 9 soak games finished
  (`ai/soak.md`).
- **Nothing in the simulation has been compared with the original
  running.** With no Wine, the port follows the original program as studied, and the
  tests check the port against the rules as read, not against the game.
  This is the main thing to decide at this gate.
- 230 tests pass, none skipped on this machine (they need the GOG install
  in `references/gog/`).

## Phase 3 exit criteria

| Criterion (project plan) | Status |
|---|---|
| For a set of test seeds the galaxy matches the original, checked against Wine captures | **Not done.** No Wine (Phase 1 decision, `decisions.md`); no captures from Windows yet |
| AI vs AI games finish | 8 of 9 soak games end; difficulty 1, seed 7 is a stalemate at 600000 ticks |
| AI vs AI games are identical across runs | Yes, by state hash |
| Replays reproduce every tick's state hash | Yes (`--verify-replay`) |

## Data layer (Phase 2, signed off 2026-10-18)

- The GOG build 2.0.0.12 is checked file by file (251 files, size and
  sha256) on import; anything else is refused (`gog-build.md`).
- GOG's executable is retail 1.072 with 16 bytes changed: the signature
  and three disc check patches. None touches the rules, so no toggle.
- Readers for the resource container and every record the game uses
  (races, hulls, strings, techs, views, palettes, pictures), preferences,
  fonts, and the tables copied from the executable (sin and cos by
  degree, combat sprite sizes). Art becomes indexed PNG, movies MP4.
- Open: the meaning of a few weapon and fighter fields (they match where
  combat uses them; `formats/tables.md`), some view widget bytes
  (`formats/views.md`), and the palette for each picture, which is a
  heuristic (`PaletteGuess`) that could be replaced from the view data.
  SpecData and the unused ShipData have no reader. Original saved games
  are not read (see decisions below).

## Decoders (Phase 2, signed off)

- Own Ogg Vorbis and WAV decoders for music and sound.
- Movies: converted to H.264 and AAC at import by the pinned ffmpeg, and
  played back by a decode-only LGPL libavcodec through our own bindings
  (Jeff's override of the project plan for video).
- Open: the listening check, put off to Phase 4 because this machine has
  no sound. Audio is checked by tests only. `Heliograph.Audio` (the mixer)
  is still empty; it belongs to Phase 4.

## Simulation (Phase 3)

How fidelity is aimed at (`approach.md`, `numerics.md`): one game random
stream with every draw in the original's order, discarded ones too; x87
double arithmetic with the original's float to int conversion; sin and
cos computed to match the x87 at 53 bits (1085900 cases checked against a
native harness, none giving a different position); the original's sort
for anything sorted. No static state, no wall clock.

| System | Doc | Notes |
|---|---|---|
| Galaxy and setup | `sim/universe.md`, `sim/players.md` | Stars, planets, wormholes, players, home worlds, starting assets |
| Time | `sim/time.md` | The ten scheduler tasks, calendar, speeds |
| Star step, movement | `sim/star-step.md`, `sim/movement.md` | Orbits, fleets, wormhole jumps, merging, colonizing on arrival |
| Ships | `sim/ships.md` | Design builder, refits, upkeep, supply |
| Economy | `sim/economy.md` | The 40 tick empire passes, budget, colonies, construction |
| Research | `sim/research.md` | Research task, techs, scientist bonus, AI redesigns |
| Diplomacy | `sim/diplomacy.md` | Attitudes, the 23 incidents, treaties, war |
| Combat | `sim/combat.md` | Setup, placement, resolving at once, retreats, stalemates |
| History | `sim/history.md` | The comparison graph samples |
| Advisors | `sim/advisors.md` | Governor growth, promotions |
| Visibility | `sim/visibility.md` | Sight per system, meeting in a system |
| Espionage | `sim/espionage.md` | Nine missions, rolls, incidents |
| Trade | `sim/trade.md` | Packages, transfers, AI answers |
| Computer players | `ai/computer-players.md` | Every module of the AI turn |

Bugs and quirks of the original that affect play are kept and marked in
the code and docs (15 noted in the docs), for example: a tech can be
learned twice; a computer player with no fleets refuses every trade
exchange; players never met count as friends to the AI. The one place the
port departs on purpose is battles nobody can win, which hang the
original: every side withdraws (`decisions.md`).

## Open items

Needing your decision (see the end):

1. How to check fidelity against the original without Wine.
2. Loading the original's saved games, listed in the project plan for Phase 3.

Left for Phase 4 on purpose (they need the game screens):

- Step by step battles for human sides (about 70 KB of original code).
- The encounter check for fleets set to intercept, which only the
  player's fleet window sets.
- Windows that change plans, funding, research and trade; the sim side of
  their orders is in place.

Known unknowns, none affecting computer player games as far as seen:

- What clears the byte+0x5C; the port treats it as set
  during play (visibility, turned advisors, elimination cleanup).
- The flag at +0x9C94 in the scheduler (`sim/time.md`), a fourth kind of
  fleet target (`sim/movement.md`), and the owner callback +0x38 for a
  destroyed fleet (`sim/star-step.md`).
- Universe creation step 4.4 is medium confidence (virtual calls not fully
  resolved; `sim/universe.md`).
- Whether a computer player's trade patience is saved (`sim/trade.md`).
- Elimination of a player who still owns something: so far only players
  with no colonies and no fleets are eliminated, which is the original's
  only path found.

From the soak runs (`ai/soak.md`): games are long (500 to 660 game years
for 4 players at difficulty 3), peaceful computer players hoard money
because they build warships only at war, and the difficulty 1 game never
ends. Each may be the original's behavior; none can be told apart without
a comparison.

## Decisions for you

1. **Fidelity check.** The exit criterion asks for galaxies checked
   against Wine captures, and Wine is out. Options: you run the original
   on Windows for a few seeds and send screenshots of the galaxy map (and
   a save, if we read saves); or accept the code-derived checks for now
   and compare later, when the map screen exists, side by side at the
   vanilla gate.
2. **Original saved games.** Read them now (a format study, then a loader;
   it would also give exact states to compare with), or defer to Phase 4
   or later.
3. **The difficulty 1 stalemate.** Look into it now, or note it and check
   it against the original later.
4. **Sign-off.** Whether the data layer, decoders and simulation are
   signed off, so Phase 4 (presentation) can go ahead in full.
