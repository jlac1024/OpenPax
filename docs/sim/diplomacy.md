# Diplomacy

How the original handles relations between players, from static study.
Confidence is high unless marked. R(n) is the game stream's Random(n).

## Scores and dispositions

Each player holds, toward every other:

- a disposition, -3 to 3, set at setup (`players.md`) and never changed;
- an attitude score, starting at disposition + 10 + R(40), moved by
  incidents, kept within 250 either way (exactly 0 becomes +1 or -1 by the
  disposition's sign). Only computer players read scores.

## Treaty states

Seven states per pair and direction: 0 War, 1 Neutral, 2 Trade Routes,
3 Repair and Resupply, 4 Allied, 5 Joint Research, 6 Share Information
(names from the foreign affairs window's strings, matching each state's
effects). Each slot is off, in force, offered by me (pending), or offered to
me (pending). The level of a pair is the highest state in force, or 200
(No Contact) when none is. A pending offer is contact but no level.

Writing a slot in force has knock-on effects: Neutral ends a war, or ends an
alliance together with Joint Research and Share Information; Allied ends
Neutral; writing War (whatever the value) clears every other state.

Effects of states: Trade Routes and Joint Research give income and
research shares (`economy.md`); Repair and Resupply lets fleets draw
supply at the partner's colonies and repair at its shipyards; an alliance
in force runs the victory check for the player writing it (all remaining
players at level above 3 ends the game); Share Information redoes every
star's visibility when it goes directly between off and in force (not on
accepting a pending offer; quirk kept).

Quirk kept: level above 3 is also reached by Joint Research or Share
Information alone, and No Contact (200) counts as above 3 wherever the
original compares levels that way.

## Offers and replies

An offer marks the slot pending on both sides (offered by the proposer,
offered to the recipient). A reply writes the same value on both sides, the
other side first: accepting (in force), turning down or withdrawing (off),
and also, with no offer at all, declaring war, dropping from an alliance to
Neutral, and canceling a treaty. In single player these run at once.

A computer player answers offers to it in its treaties and trade module
(`ai/computer-players.md`) by its score toward the proposer: it accepts
state 1 above -1, 2 above 0, 3 above 74, 4 above 99, 5 above 144, 6
above 174, never War.

First contact (colonizing next to another player) puts Neutral in force
both ways (`economy.md`).

## The diplomacy task

Every 200 ticks, for each ordered pair of players who have met (level not
200): the average strength of each (colony structure strength plus fleet
strength, over the number of colonies; 0 with none) is compared, and if
the other's is higher, the score toward it rises by 5 percent of the gap;
then the score moves by the disposition. No draws.

## Incidents

| Kind | Score change | Raised by |
|---|---|---|
| 0, 1 | recipient toward partner +0.1 | trade routes, joint research in force (economy) |
| 2 | toward the other + disposition | diplomacy task |
| 3 | toward a stronger player + 5% of the gap | diplomacy task |
| 4 | +20 toward the attacker | a player at war with an attack's victim |
| 5 | -20 toward the attacker | a player at level above 3 with the victim |
| 6 | -5 | a spy seen failing to get in (`espionage.md`) |
| 7 | attacker +d, victim -d (d population lost) | a colony taken or bombarded (combat) |
| 8 | killer +2, victim -5 | ship destroyed (combat) |
| 9 | attacker +1, victim -3 | structure destroyed (combat) |
| 10 | victim -40 toward the attacker | a fleet attacks a planet on arrival |
| 11 | victim -20 toward the attacker | a fleet attacks a fleet on arrival |
| 12 to 21 | by item value | a trade package accepted |
| 22 | -5 both ways | a trade package rejected |

Kinds 10 and 11 are followed by the third-party reactions 4 and 5, from
the victim's view of the levels.

## Not yet ported

Nothing: trade packages (12 to 22) are in `trade.md`. Pending offers go
on elimination with the treaties.
