# Trade packages

A trade package (0x68 bytes) is an offer from a proposer to a
target with two halves: what the proposer gives and what it asks the
target to give. Each half names planets (by object id), techs (by id),
ships (by ship id), a star map flag and whole credits.

Only a human's trade window makes packages. Computer players
never propose one, so games of computer players only never see them.

## Offer, accept, reject

Packages travel as messages with an action:

- **0, offer:** a copy goes to the front of the target's pending list
  (+0x800, newest first). Nothing is checked. The local target gets a
  notice (`TradeOffered`).
- **1, accept:** both halves must still hold: the
  half's player is still in, every planet named has that player's colony,
  every tech is known, every ship is found in a fleet of that player, and
  the credits are no more than its treasury. If not, nothing moves and no
  incident follows. If they hold, in this order, each value worked out
  just before its transfer and then given as incident 12 to 21
  (`diplomacy.md`), proposer first:

| Kind | What | Value |
|---|---|---|
| 12 | offered ships to the target | ships value for the target |
| 13 | asked ships to the proposer | ships value for the proposer |
| 14 | offered planets | 5 * sum of planet scores |
| 15 | asked planets | likewise |
| 16 | offered techs | 10 per tech the target does not know |
| 17 | asked techs | 10 per tech the proposer does not know |
| 18 | offered credits | credits * 0.00002 |
| 19 | asked credits | likewise |
| 20 | offered map | 5 per system the proposer has seen and the target not |
| 21 | asked map | the other way round |

- **2, reject:** incident 22, -5 both ways.

Accepting or rejecting then removes the first pending package that
matches. Quirk kept: two packages fail to match on their
players, credits and map flags only when all six differ, so in practice
only the planet, tech and ship lists are compared, and an answer can take
away a different package with the same lists.

The ships value is 0 unless the receiver is a computer player; then each
ship counts max(0, (0.9 * strength of the receiver's own design in the
ship's slot - the ship's strength) * 0.1 + 1), skipped when that slot is
empty. Strengths are whole numbers (`ships.md`).

## Transfers

- **Ships**: if any are named, the receiver
  makes one new fleet named STR# 82 entry 7 ("Trade Package Fleet") at its
  newest colony, else at the first planet of the first system (drawing
  its orbit). Each ship found leaves its fleet. Quirk kept: a computer
  player's ships are deleted, so it gets an empty fleet, dropped by the
  next star step. For a human, at every 21st ship another such fleet is
  made and left empty, and the first fleet, full, refuses the 22nd ship
  on, which are lost (a bug kept).
- **Planets**, each still the giver's
  : the improvements' construction, the unit count and hit
  points of the stacks of kinds 5 to 10 and the population are noted; the
  colony is destroyed (which may eliminate the giver) and founded again
  for the receiver at generation 1. Then each noted improvement gets a
  slot if needed and its construction is fed back in as building points
  (a quirk kept: points and construction differ); each stack is rebuilt
  unit by unit with the receiver's best tech of that kind and given the
  noted hit points as building points; the population is set back, up to
  what the planet holds for the receiver.
- **Techs**: each one learned as through
  espionage (`espionage.md`).
- **Credits**: taken from the giver, then given.
- **Map**: every system the giver has seen and
  the receiver never has gets a sight update showing it to the receiver
  (`visibility.md`).

## Computer players

In a computer player's treaties and trade module, after it answers
treaty offers and before it cancels and offers treaties
(`ai/computer-players.md`), every package waiting for it is answered at
once, oldest last:

1. A gift (something offered, nothing asked, both still in): accepted, no
   draw.
2. Not a demand (an exchange, or anything with an eliminated player):
   gain is the sum of values 12, 14, 16, 18, 20 for it, cost of 13, 15, 17,
   19, 21. The bar is 0 at war or with no treaty, gain - cost + 20 at
   levels 1 to 3, gain - cost + 50 at 4 to 6. r = R(100); accepted when
   r + 1 is below the bar and the veto does not fire.
3. A demand (nothing offered): patience toward the proposer (30 at the
   start, kept per proposer on the computer player) drops by 10, always.
   Rejected unless the proposer's average strength (`diplomacy.md`) is
   above 1.5 times its own; then r = R(100), accepted when r + 1 is below
   the patience before the drop and the veto does not fire. So a demand
   from a much stronger player is met 29%, 19%, 9%, then never.

The veto: the ships asked are worth at least a quarter of
all its fleet strength, or the planets asked a tenth of the strength of
all its colonies. Quirk kept: with no fleets it vetoes every exchange.

Packages never expire; they leave only by an answer or elimination.

## Elimination

Packages an eliminated player offered are removed from everyone's lists,
and its own list is emptied.

Open: whether patience is kept in saved games.
