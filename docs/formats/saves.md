# The original game's saved games

Files in the original's "Saved Games" folder, named as the player typed
them, with no extension. Loader: `src/OpenPax.Sim/Saving/Original/`.
Only single player saves of version 1.072 are read, as the original reads
only those. Copy them into the port's "Saved Games" folder (in the user
folder) and they are listed with the port's own saves.

Not yet checked against a save made by the original: no such save was at
hand, so the loader is tested with files written to this description.

## The stream

- A 16-bit mark "II" (little endian). "MM" means every number is byte
  swapped; it looks like Mac support and is read the same way, swapped.
- Numbers are 32-bit or 16-bit integers, bytes, and doubles (as two 32-bit
  halves, low first: plain IEEE little endian).
- A string is a 16-bit length then the bytes (Windows-1252), with no
  terminator.
- Most blocks end with the number 1997 (0x7CD), which the original reads
  and never checks.
- Lists are a count then the items in order. Arrays with gaps (designs,
  improvements) are a count of the filled entries, each with its index.
- Nothing points: universe objects are named by object id, stars by index,
  players by index, techs by id, -1 for none. Links are made once the
  whole file is read.

## Order

1. Mark, the magic "mGxP" (single player; "NtxP" is a network save), the
   version 0x430.
2. A flag byte, the second id counter (advisors, designs, ships), a number
   not yet understood.
3. The game: the setup seed, difficulty, the year and calendar step, the
   universe (every star with its center object, planets, wormholes and
   each player's sight of it; then the star in view and the selected
   object), two flags (the second: the game is over), the players, a byte
   not yet understood.
4. The next object id.

Each player is tagged human (898) or computer (989) and holds, in order:
the empire (name, flag, fleet color, index, treaties, the relation and
trade figures toward each player, spending, upkeep, income, eliminated),
the species (laid out as a RACE record), design counters, best armor,
money and points, the plan library, research (projects, progress kept per
tech, known techs as 900 bits, shares, counts), spy missions (then the
mission counter), the fleet counter, upkeep figures, history (50 samples
of the ten series, sample by sample), trade packages offered to it,
advisors, colonies (with improvements, orbital structures and the
shipyard queue), fleets (ships, orders, mode, intercepting), designs, the
design being made in the Ship Design window, interface settings, caution
and relations.

Ships and designs share one layout; a ship marked for refit carries its
new design nested inside it.

## What a load does not bring back

The original saves no random state and draws numbers while loading (a
planet's animation frame and its 18 satellites' three draws each, a
wormhole's frame), so a loaded original game never goes on as it would
have. The port starts the stream again from the saved setup seed and
makes the same draws, so the same file always loads the same way.

Lost on load in the original, and so in the port: the satellites'
angles (drawn again), whether a wormhole is known, a fleet's retreat and
speed boost, the order of fleets in a system (they go in by player, then
fleet), every task's countdown, the computer players' patience and
working state, component built counts, battles (a save is refused during
one). The port sets its tick count from the calendar, and each player's
home star from its oldest colony, neither being saved.
