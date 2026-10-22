# Numerics and randomness in the original

First pass, from static analysis of the 1.072 executable. Addresses are VAs.

## Random numbers

One global linear congruential generator, in game code (not the C
runtime; the runtime's `rand` is not linked at all).

- `Random(n)`. Each call:
  1. adds 1 to a call counter (uint32),
  2. sets the state (uint32) to `state * 1103515245 + 12345`,
     wrapping at 32 bits,
  3. returns `((state >> 16) & 0x7FFF) mod n`, using an unsigned divide.
     `n == 0` would fault; callers must never pass it.
- Seeding stores the state directly. Its one caller
  copies the seed from field +4 of the game setup structure,
  so the seed is part of a game's settings.
- The call counter is reset. It is probably used to check
  that multiplayer peers stay in step (to be confirmed with NetMan).
- Wrappers around `Random` start. There are
  109 direct call sites of `Random` and more through the wrappers.

Because there is one shared stream, the order of every draw matters. The
port has to make the same draws in the same order, including draws whose
results are thrown away.

## Floating point

- Game code uses x87: about 5,700 of 238,000 instructions. Mostly loads,
  stores, compares, multiplies and subtracts. Six `fsqrt`.
- The precision control word is only changed inside the C runtime
  (`_ftol` truncation and `_controlfp`). The game never sets
  it, and DirectDraw does not change it (only Direct3D would). So the
  game runs at the Win32 default: 53-bit significand, round to nearest.
  Basic arithmetic then matches IEEE double, apart from the wider exponent
  range while values stay in registers.
- Float to int goes through `_ftol` after subtracting 1 from negative
  values: floor for fractions, but -3.0 becomes -4 (`X87.ToInt`).
- Transcendentals use x87 instructions directly: `fcos`/`fsin` for orbit
  positions and wormhole placement (
  ), and `fpatan` for directions (also used by AI
  code). They return 64-bit mantissas whatever the precision control; the
  `fimul`/`fmul` that follows rounds once to 53 bits. Angles become radians
  as degrees * 3.141593 * (1/180): the game's pi is 3.141593.

### How the port reproduces them

`OpenPax.Sim.Numerics.X87` computes sin, cos and atan2 in double-double
arithmetic (about 106 bits, plain IEEE +, - and * only, no fused
multiply-add), rounds to a 64-bit mantissa like the x87 register, then
multiplies and rounds to 53 bits as the original's next instruction does.
Platform math libraries are never used for results, so every platform
gets the same bits.

`tools/x87_reference.c` runs the real instructions under the game's control
word for every starting orbit angle at every orbit radius, a million
random later angles, and 200,000 star-to-star directions. Against it
(AMD Ryzen 9 3900X): 1,085,900 cases, 193 differ in the last bit, none
gives a different integer position. The last-bit cases sit near zeros of
sin and cos, where x87's own argument reduction (a 66-bit pi) is inexact.
x87 results can differ between CPU makers in the same way, so the original
itself was never more exact than this.

## Other random sources (not the LCG)

- seeds the LCG from the C runtime's `time()` once at
  startup, from the application object's constructor.
- builds a number from `GetSystemTime` fields. It picks the
  title screen variant and the default seed in the galaxy setup screen
  (that seed modulo 25000).
- mixes that value with `timeGetTime`. It never touches the
  LCG and only picks explosion sprite variants and voice clips, so it is
  cosmetic. The port can use any source here.
- The galaxy setup screen (constructor) reseeds the LCG to draw
  its preview, and "new seed" draws `Random(10000)`. Game setup
  and universe creation reseed it again from the
  game's seed, so preview draws never reach the game.
- The wormhole constructor draws `Random(45)` from the LCG
  for its first animation frame. It is cosmetic but uses the shared
  stream, so the port must make the same draw.

## Time and the simulation

Wall-clock time paces the game but does not decide anything in single
player:

- The game clock runs one simulation step whenever its
  interval has passed. In strategic mode the interval comes from the game
  speed setting: 600, 450, 450, 150, 75, 38 or 13 ms. Combat
  steps every 40 ms. No elapsed time is passed into the step, so the
  simulation advances in whole ticks.
- A scheduler runs tasks with countdowns in ticks. The AI is
  one task (method, every 20, 10 or 5 ticks by difficulty; see
  `sim/time.md`). It calls the AI_* modules
  directly on the main thread with no time budget, so the AI is
  deterministic for a seed and input stream.
- The `timeGetTime` calls once thought to be in AI code ( and
  nearby) are in animated widget and spinner classes that sit between
  AI_TAndT and CheatWin in link order. They only drive animation and
  hold-to-repeat input.
- About 60 other call sites drive animation, UI timers, camera scrolling,
  palette fades and network timeouts.
- Exception, multiplayer only: every 5 syncs the host measures time per
  sync and changes how many ticks run between syncs (NetMan,
  setter). Orders are applied at sync boundaries, so the tick
  an order lands on depends on network timing. See `netcode/original.md`.

The port keeps the same structure: integer ticks paced by real time, with
everything that decides outcomes inside the tick.
