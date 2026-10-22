# Star visibility

Every star system keeps one sight level per player (+0xB8, 16 ints):

| Level | Meaning |
|---|---|
| 0 | never seen |
| 1 | seen before, not in view now |
| 2 | in view now |

Only drawing code reads it, always for the local player (planet owners,
fleets, map markers). The simulation does not, but the update that sets
it also makes players meet, which does matter. No draws.

## The update

Runs for one system whenever something there changes:

- a fleet enters (a new fleet, or one arriving through a
  wormhole) or leaves (jumping out, or dropped by its owner);
- a colony is founded or destroyed;
- for every system, when a Share Information treaty goes directly between
  off and in force;
- espionage (`espionage.md`) and trade packages (
  ), which pass a player who is shown
  the system. Trade packages go through the map transfer (`trade.md`).

A player is present in a system when not eliminated and owning a planet
or a fleet there. Steps:

1. A player shown the system is set to 1.
2. Each player present is set to 2; each one not present but at 2 is set
   to 1. Others are left alone.
3. For each pair i < j where i's Share Information slot toward j is in
   force: if j is present, i is set to 2; if i is present, j is set to 2.
   Only i's treaty is read, for both directions.
4. For each pair i < j both present whose treaty level is none (200),
   Neutral is put in force both ways, and the local player is told it
   has met the other.
5. Each player whose level changed gets the message to redraw the system
   (virtual +0x2C, posting 0x408; the same for both player classes).

Setting a level gives 2 instead to a Psychic species or an
eliminated player. So those never lose sight once a system is set for
them; they do not see systems no update has touched.

The whole update is skipped when the app's game-running byte
(+0x5C) is clear. It is set when a game is made, before setup
, and cleared when the game is torn down, so it
is set throughout setup and play; the port has no such byte.

New systems start at 0 for everyone.

## Meeting

Players meet in two ways: through step 4 here, and when a colony is
founded, with players present one wormhole away (
`economy.md`). Treaties written as war, as tests do, are not "none" and
do not meet.
