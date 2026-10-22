# New game and universe generation

How the original builds a new game, from static study (Phase 3). This is
the first fidelity target: a seed and star count must give the original's
galaxy. Order of random draws is everything; every `Random(n)` below is the
game LCG (see `numerics.md`). Confidence is high for sections
2 and 3 and steps 4.1 to 4.3, medium for 4.4 (some virtual calls not yet
resolved), low for the setup screen's control ranges.

## 1. Setup structure

| Offset | Meaning |
|---|---|
| +4 | seed (default from the clock mod 25000; the setup screen's random button gives `Random(10000)` from the current stream) |
| +8 | star count, at most 100 |
| +0xC | total players minus 1 |
| +0x10 | human players (humans come first in the player list) |
| +0x14 | difficulty 1 (easiest) to 5 |
| +0x18 | player info array; the game writes the AI entries into it |

## 2. Game setup

1. Store the seed and difficulty; `Seed(seed)`.
2. For each AI player in order: species `Random(8)` (index into the RACE
   list, repeats allowed); fleet id candidate `Random(16)`; flag
   candidate `Random(30)`; name `Random(30)` redrawn until unused
   (STR# 46000); then the flag redrawn with `Random(30)` while taken, then
   the fleet redrawn with `Random(16)` while taken.
3. Create players, humans first. AIs get a research split
   with its own draws; its control flow must be checked against the
   original program before porting.
4. Initial relations: `Random(6)` per other player, then
   `Random(40)` per other player.
5. Create the universe (section 3). It reseeds, so the galaxy depends only
   on seed and star count.
6. Place homeworlds and starting assets (section 4), continuing the stream.

## 3. Universe

1. `Seed(seed)`. For each star i: `x = Random(450)`, `y = Random(450)`,
   both redrawn until 20 <= x, y <= 430 and the 30x30 box around the point
   overlaps no earlier star's box (|dx| < 30 and |dy| < 30 overlaps).
2. `Seed(seed)` again; star type `Random(7)` for each star.
3. Star name `Random(102)` for each star, redrawn until unused (STR# 1000;
   its 103rd name, "Zeus", can never be picked).
4. For each star (sector init): planet count
   `Random(3) + Random(3) + 2`; then per planet, its constructor
   draws an animation frame `Random(20)` and 18 satellites,
   each `Random(5) + 20` radius, `Random(3600)` angle (tenths of a degree)
   and `Random(100) < 30` for reverse direction; then each planet's orbit
   slot `Random(6)`, redrawn until free; planets are then taken in slot
   order. Planet init per planet, inner to outer:
   - temperature = (star type - slot + 3) * 25;
   - size: if temperature <= 0, `Random(2)`, non-zero means type 4;
     otherwise (or if that draw was 0) `Random(3) + 1`; a forced last
     planet turns type 4 into 3;
   - `Random(5) + 1` (a rating, +0x204);
   - moons `Random(type)`;
   - class 5 for type 4, else `Random(5)`;
   - orbit: angle `Random(3600) * 0.1`, speed 50 / radius, reversed if
     `Random(100) < 30`; one position update;
   - `Random(3)` once per moon, summed, plus `Random(5) + 1`, stored as a
     double (+0x208).
   Orbit radii by slot: 38, 64, 90, 116, 152, 178.
5. Wormholes: all star pairs with integer distance
   truncate(sqrt(dx^2 + dy^2)), sorted by distance with **MSVC's qsort**
   (not stable; the port must reproduce its tie order); a spanning tree is
   grown by repeatedly taking the first edge with exactly one connected
   end; each star then gets as many wormholes as tree edges touch it, each
   drawing `Random(45)` (animation frame); edges are linked in sorted
   order.
6. Wormhole positions in the system view: angle toward the destination
   star, position (220, 220) + 175 * (cos, sin). No draws.

## 4. Homeworlds and starting assets, per player

1. Home star: up to 10 rounds of 10 candidates `Random(N)`
   (redrawn until free), accepting one whose wormhole distance to every
   earlier home is at least a minimum that starts at N / players and
   shrinks; then 10 tries requiring 2 hops; then any free star.
2. Home planet: the first, in orbit order, minimizing
   |T - preferred + tolerance|. This looks like a bug (|T - preferred| was
   probably meant) and is kept.
3. Make it the homeworld: type 3, rating 5, 2 moons, a second
   orbit update, class from the species' atmospheres, temperature set to
   the preferred one, +0x208 set to 11 (AI) or 11 - 2 * (difficulty - 1)
   (human).
4. Starting assets: AI designs (with `Random(100)` draws),
   colony (`Random(10)`), two structures, fleets (each orbit draws
   `Random(3600)` and `Random(100)`): an AI gets difficulty-many fleets of
   one ship; a human gets a fleet sized by difficulty plus one more fleet.
   At difficulty 5 the human's first fleet is empty, which looks like a bug
   and is kept. Build queues complete instantly.

## Floating point on this path

All doubles. `fsin`, `fcos` and `fpatan` produce 64-bit mantissas whatever
the precision control; the following multiply rounds to 53 bits. Angles in
degrees become radians as `deg * 3.141593 * (1/180)`: the constant is
3.141593, **not pi**, and must be kept. Conversions to int use
F(v) = truncate(v - (v < 0 ? 1 : 0)): floor for fractions, but -3.0 becomes
-4. Planet position: F(center + cos(rad) * radius), F(center + sin(rad) *
radius). Wormhole angle: atan2 in degrees using 1/3.141593, plus 360 when
negative. Distances use exact integer squares and sqrt.

## Audit

The port (`OpenPax.Sim.Galaxy.UniverseGenerator`) was checked against
the original program by an independent pass: every draw, loop bound, object id,
tree step and floating point operation matches for 2 to 100 stars. Notes
from it:

- With one star the original crashes (a placeholder edge links star 0 to
  itself, then a null partner is followed when placing wormholes). The
  port refuses fewer than 2 stars.
- If a second game in the same session has the same star count, the
  original reuses its old star array: stars keep their old object ids and
  planets are added to the old planet lists. Whether teardown frees it is
  not yet checked. The port always starts each game fresh.
- `Random` also counts its calls; nothing on this path reads
  the count.
- x87 transcendentals are not correctly rounded and differ between CPU
  makers; see numerics.md for how close the port is.

## Outside state and quirks kept

- Object ids come from a global counter reset only between games; within a
  game: stars, then planets, then wormholes, then fleets.
- Each object stores timeGetTime; it is excluded from state hashes.
- The setup structure is modified in place (AI player entries).
- Kept bugs: the homeworld temperature metric, the empty difficulty-5
  fleet, the double orbit step on the homeworld, 3.141593 for pi, qsort tie
  order, 16-bit loop counters in the wormhole tree, F(-n.0) = -n - 1.
