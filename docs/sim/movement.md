# Fleet orders and movement

How fleets move, from static study. All movement happens inside a star
system, in its view coordinates. A fleet changes system only by jumping
through a wormhole, which is instant. Confidence is high unless marked.

## Orders

A fleet has a queue of orders (+0x90). An order targets an object (fleet,
planet, wormhole or the star's own object at the system's center,
220, 220) or a point. Its
target position is the object's current position, or the point.

- Adding an order: if the queue is empty the order starts it
  and the fleet leaves its orbit (no center). Otherwise it goes on the end,
  unless its target position equals the last order's, in which case it is
  dropped.
- Removing an order: when the queue becomes empty, a fleet in
  colonize mode whose last target is a free planet that is not a gas giant
  uses up a colony ship to found a colony. The fleet's mode
  goes back to none.

Fleet modes (+0xA4): 0 none, 1 colonize, 2 attack.

## Directions and distances

Direction from a to b in degrees: with equal y, 0 if a.x <=
b.x else 180; with equal x, 90 if a.y <= b.y else 270; otherwise
atan2(dy, dx) * 180 * (1 / 3.141593), plus 360 when negative. Distance is
the square root of the integer sum of squares.

Movement uses two tables of 360 sines and cosines by whole degree. They
are six-decimal values typed into the original, about a fifth of them a
step off true rounding (sin 2 degrees is 0.0349). The importer copies them
from the executable into the converted data.

## Each tick

Run for every fleet not held in combat, after the encounter check.

1. Budget = the fleet's speed; while the boost countdown (+0xAC) is above
   0 it is speed times 10 and the countdown goes down by one.
2. While budget > 0:
   - With no order: one orbit step if the fleet has an orbit center; the
     budget is used up.
   - Otherwise: d = direction to the target as a whole degree (truncated);
     dist = distance to the target. For the last order with a target
     object, stop short: 30 for a planet or the star; for a fleet, 5
     when it is the same owner's or at a treaty level above 1 and the fleet
     is not attacking, else 50; never below 0.
   - step = the smaller of dist and budget; budget -= step.
   - x = trunc(cos[d] * step + x + 0.5), y likewise with sin (the add of
     0.5 is written as a subtraction of -0.5).
   - If step equals dist, the fleet has arrived (below).

## Arrival

For the last order in the queue:

- No target object: the order is removed.
- A fleet of the same owner: ships move one at a time, newest first, into
  the target until it holds 21; if this fleet runs out, the order is
  removed (the empty fleet goes in the next star step).
- A fleet of another owner, both with ships: hostile when attacking (which
  declares war both ways) or at treaty level below 2; then combat
  (`combat.md`).
- A planet: hostile under similar rules (they differ for computer and
  human owners); then combat. Otherwise the fleet enters orbit around it
  at radius 30, drawing R(3600) and R(100) as every new orbit does, and its
  angle is then set to the direction from the planet to the fleet.
- The star's own object: enters orbit around it the same way.
- A wormhole: with jumps left, every ship's jump count goes up, the fleet
  moves to the partner wormhole's position, orbits it, leaves this system
  and joins the partner's (which also checks supply), and both ends are
  marked as known. With no jumps left, it stops at the wormhole: a human's
  fleet orbits it, a computer player's sits still (orbit with no center,
  still drawing), and all its orders are dropped.

For an order that is not the last, only a wormhole does anything (the
jump, as above); the order is removed and the next one starts.

## Encounter check

Only for fleets with the intercept flag (+0xA0), which only the player's
INTERDICT order turns on. Each tick, before it moves, such a fleet (not in
battle) looks at every other player's fleet in its system, newest first:

- Hostile: its owner is at war with them (treaty level 0), or neutral
  (level 1) and the other fleet's first order makes for one of the
  owner's colonies, one of the owner's fleets, or a wormhole whose far
  system holds one of the owner's colonies. The interceptor drops its
  orders, draws a new orbit (radius 30, no center: R(3600), R(100)), is
  ordered onto the hostile fleet, and looks no further.
- Not hostile: if the interceptor's first order is chasing another
  player's fleet, it drops its orders and draws a new orbit (quirk kept:
  meeting any friendly or neutral fleet calls the chase off).

Computer fleets never intercept, so games between computer players draw
nothing here.
