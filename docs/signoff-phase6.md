# Phase 6 (port quality): ready for sign-off

The project plan's exit: every toggle works on its own, and with all toggles off
the game behaves like Phase 4. (Legacy netplay is dropped, decisions.md.)

## Done

- **Config file**: heliograph.toml in the user folder, written within a
  second of a change and on closing; the player's name kept.
  config.json beside the game names the public lobby.
- **Resolution, widescreen, UI scaling**: the original screens draw at a
  whole-number scale of the window, sharp. Nine game screens grow into a
  wide window (docs/ui/widescreen.md); the bridge and setup screens stay
  4:3 between starfield bars. The port's own screens are true-color forms
  that fit any window.
- **Frame pacing**: the display's refresh rate, vsync by default
  (docs/ui/frame-pacing.md).
- **Gameplay toggles**: speed 0 pauses (on, Jeff's call), with a vote in
  multiplayer; twelve fixes to the original's bugs, each its own setting,
  off by default (docs/sim/fix-candidates.md).
- **Presentation settings**: upscaled art, text font and outline, moving
  menu starfield and its speed, a made sky per system, movies.

## Checked

- Each fix on its own, and off, in `FixesTests` (Sim) and the Options
  tests (Ui); a game with every fix on plays the same each time.
- All toggles off: the 30,000 tick, six player headless game on seed 1234
  still ends on state hash 2a406db3c98287eb, as at the engine sign-off,
  and a replay recorded on 2026-10-19 matches all 20,000 ticks. Fixes enter
  the state hash only when on, so games as the original keep their hashes.
- Over 500 tests run and pass (488 test methods, some run for several cases).

## Not checked yet

- How it looks and feels in a real wide or 4K window (renders only).
- The LAN test with real machines (docs/netcode/lan-test.md), Phase 5's
  exit, still with Jeff.
