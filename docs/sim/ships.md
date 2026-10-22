# Ship design, ships and fleet stats

How the original builds designs, turns them into ships and works out a
fleet's speed and range, from static study. Confidence is high unless
marked. R(n) is the game stream's Random(n).

## Tech records as the game reads them

The game reads TECH fields 8 bytes further on than our reader's offsets
(the cache hands out a pointer to the record object's start). Fields used
here: research cost, cost (summed into a design's cost; the TECH format
doc used to call it "rating"), hit points, size, and per class: star drive
rating and largest hull class; tactical drive combat speed and defense
bonus; weapon damage bands, reload and weapon type; shield type,
capacity and percent; armor structure multiplier; fighter weapons and two
more fields.

Kind lookups walk the techs of one kind in id order; all techs of a kind
are contiguous in the file. "Known" is the player's known set; tech id 0
counts as known and -1 as not.

## Tech level, size and cost

Level(x) is floor(cube root of x), 0 for negative x (a binary search over
a table of i cubed for i = 0 to 304).

A player's research total is the sum of the raw research costs of every
tech given to them through the give-tech path. Free starting
techs add 0. An AI given techs 2 (25000) and 3 (100000) is at 125000,
level 50.

Size of a tech for a player: with no player, or for a
planetary improvement, the raw size. Otherwise
pct = (Level(raw research cost) - Level(research total) + 215) * 5,
clamped to 500..1000, and size = raw size * pct / 1000 (integers). Sizes
stay full until the player is 16 levels past the tech, then shrink half a
percent per level, down to half. Cost scales the same way.

## Designs and ships

Designs and ships are one class (copy). A design
has a hull type 0 to 5 (HULL 2000 + type), a quality index, nine
component slots, names, and derived values: cost, used space, raw space
and structure. A ship is a copy of a design with its own id, an owner,
experience, jumps since supply and a fleet.

Slots by tech class: star drive 0, tactical drive 1, sensor
2, ECM 3, armor 4, shield 5, weapons 6, fighters and improvements 7,
specials 8. Slots 0 to 5 hold one component; 6 to 8 are lists.

A component has a tech, a weapon mode (-1 for anything not a weapon; the
builder uses 0 to 3), a count, damage taken and its count when built.
Adding one replaces whatever is in a single slot; in a list
it adds to the count of an entry with the same tech and mode, or goes on
the end. Copying a design appends without merging.

Derived values: cost and used space sum scaled cost and size
times count over the slots in order; raw space uses unscaled sizes.
Structure: quality factor from {0.5, 0.75, 1.0, 1.5, 2.0,
2.0, 1.75, 1.5}, times the armor's multiplier if any, times the hull's
structure, rounded half up.

Condition of a component: 100 - damage * 100 /
(count * hit points), integers.

## The design builder

For hull type t with capacity cap; rem starts at cap and every added
component takes its scaled size off it (it may go negative). A fraction
budget is a whole number: trunc(base * fraction). "Last known" means the
highest-id known tech of the kind that passes the test.

| Fraction of | 0 | 1 | 2 | 3 | 4 | 5 |
|---|---|---|---|---|---|---|
| tactical drive | .3 | .3 | .1 | .3 | .3 | .25 |
| sensor | .05 | .05 | 0 | .05 | .05 | .02 |
| ECM | .05 | .05 | 0 | .05 | .05 | .05 |
| shield | .1 | .1 | 0 | .1 | .1 | .05 |
| fighters | 0 | 0 | 0 | 0 | 0 | .43 |
| planet bombs | 0 | .06 | 0 | .06 | .06 | .02 |
| first energy weapon | .2 | .2 | 0 | .2 | .2 | 1.0 |
| weapons, mode 1 | .15 | .15 | 0 | .15 | .15 | 0 |
| weapons, mode 2 | .15 | .15 | 0 | .15 | .15 | 0 |

1. Set the hull; components already on the design stay (a human's armor).
2. A computer player's design gets its type's name (string list 26000).
3. Star drive: the last known one whose largest hull class fits (2: types
   0 to 2; 3: type 3; 5: types 4 and 5). No size test; a budget is
   computed and ignored.
4. Tactical drive, sensor, ECM: the last known of the kind whose size fits
   cap times its fraction.
5. Shield: the same against cap times its fraction, trying fields, then
   shields, then screens; the first kind that gives one is used.
6. Quality index: 0 for type 2, else 4.
7. Minesweeper, computer players only, not types 0 and 2: types 4 and 5
   always; types 1 and 3 draw R(100) and want one when R(100) + 1 <= 30.
   Then the last known minesweeper, if any. Types 4 and 5 make no draw.
8. Fighters (budget from cap): repeatedly the last known fighter that
   fits, until none does.
9. Type 2: the last known colonizer, no size test.
10. Planet bombs (budget from cap): repeatedly the best scoring that fits,
    mode 0.
11. From here budgets are fractions of rem as it now stands.
12. Repeatedly the first energy weapon (lowest id of the kind) if known and
    it fits, mode 3.
13. Repeatedly the best scoring weapon that fits, mode 1; then the same
    with the next fraction, mode 2.
14. Except type 2: repeatedly the best scoring weapon that fits in rem,
    mode 0, until nothing fits.

Best scoring looks at energy weapons, projectiles, missiles and area
weapons, in that order and by id: score = (sum of the four damage bands)
/ reload / scaled size * (1.3 for missiles, else 0.5), in doubles. A
candidate replaces the best when its score is greater or equal, so ties
go to the later one.

Every design is built by this one routine: a human's types 0 to 2 at the
start (blank design with the best armor added first), and
every computer player design whenever it learns a tech that is not an
improvement or installation (each slot is rebuilt from a
blank design of its type, so computer designs never have armor).
Putting a design in a slot gives it a class name (string
list 20005 by type plus a per type counter, which then goes up) and a new
id from the second counter.

Only step 7 draws.

## Ships in fleets

A ship is a copy of the design in a slot with a new id. A
fleet holds at most 21 ships, newest first. When a ship
joins a fleet whose owner is a warrior species it gains experience 2 at a
time until it reaches level 2. The owner's upkeep grows by the ship's
cost (times 0.8 for nomads). Experience levels: points (plus a fifth for
warriors) over 100, 1000, 1500, 2500, 4000, 6000 give levels 1 to 6.

## Fleet stats

Run when the fleet is made, when a ship joins or leaves, on a jump, on
resupply and in upkeep. Over the ships, newest first:

- speed = the lowest combat speed: the tactical drive's
  combat speed times (condition / 100 as an integer, so a damaged drive
  gives 0; quirk kept), times 1.2 for aquatic species; 0 with no drive.
  An empty fleet has speed 0.
- jump range = the lowest star drive range: trunc(condition
  * rating * 0.01) when the hull fits the drive, else 0.
- jumps used = the highest of the ships' jumps since supply.
- strength = the sum of the ships' strength ratings (used by
  diplomacy and AI): defense from hull, experience and drive, times
  structure, plus shield, plus undamaged weapon damage by mode and band,
  plus fighters; the square root, truncated.

Jumps left = max(0, range - jumps used). A jump adds 1 to every ship's
count; being at a star with supply resets them (supply test not yet
studied).

The starting ships of a human (types 0 and 2) have combat speed 2 (2.4
for an aquatic species), range 2 and structure 200 and 100.

## Open

- What weapon modes mean in combat.
- Two ship fields and two component fields copied but not yet read.
- The supply test.
