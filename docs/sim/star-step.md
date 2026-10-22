# Star system step

The scheduler's every-tick task (`time.md`) visits every star
system in index order and runs on it. From static study.

## Per system

A system holds three lists, each newest first:

| List | Next link | Holds |
|---|---|---|
| +0xA8 | +0x214 | planets, innermost first |
| +0xF8 | +0x84 | fleets present (added at the front) |
| +0xAC | +0x94 | combat markers, made only by combat |

1. Every planet takes one orbit step (virtual +0x40, which for planets is
   the plain orbit step).
2. Every fleet, the next link read before the fleet is handled:
   - with no ships ( counts them): its owner removes it
     (below);
   - otherwise, unless it is held in combat (+0x74 set, virtual +0x30), it
     checks for an encounter with the other fleets here (
     movement.md) and
     then moves.
3. Every combat marker takes its step (virtual +0x40).

## Orbit step

Only for an object with an orbit center (+0x48). The angle advances by
the speed and is wrapped to 0..360; the position is the
center object's current position plus radius times cos and sin of the
angle, each rounded as `numerics.md` describes. It then sets a 40 by 40
box around the position, which is only used for mouse hits.

Planets orbit their star. A human's starting fleets orbit their
homeworld at radius 30 and so follow it; an AI's starting fleets are
placed on the homeworld's position with no center and stay there.
Wormholes have no center and never move.

## Removing an empty fleet

Called with a flag of 1 from the system step. The owner takes the fleet
out of its fleet list (+0x6FC, linked by +0x88), runs its own virtual
+0x38 on it (not yet known), every fleet of every player forgets it as a
target, and the fleet is destroyed, which also takes it out
of its system. Then the game's elimination and victory check runs
(flag 0).

A human's first starting fleet is empty at difficulty 5 (`players.md`),
so it is removed on tick 1.

## Elimination and victory check

1. A player not already out with no colonies (+0x6F8) and no fleets
   (+0x6FC) is eliminated (flag +0x4F0 set, treaties with
   everyone reset to none, its colonies and the rest removed).
2. Unless the game is already over (+0x9C96), it is taken as over, and
   then reopened by any pair of players still in whose treaty level is
   3 or less, or none. If it stays over, the game ends: the local player
   sees PlayerVictory if still in, DefeatMovie if not. Otherwise, if
   someone was eliminated in step 1, the local player sees DefeatMovie
   if it was them, or OtherPlayerElim and a message.

The treaty level is the highest of seven states 6 down to 0
set for that pair, or 200 when none is (no contact). Nothing at setup sets
one, so at the start every pair is at 200 and keeps the game open.

Quirk kept: with no pair of players left in, nothing reopens the game.
A lone human at difficulty 5 loses the empty starting fleet on tick 1
and wins on the spot.

## Fleet movement

See `movement.md`. Speed and jump range come from the ships (`ships.md`).
