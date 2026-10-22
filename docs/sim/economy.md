# Economy: the empire task, colonies and colonizing

How the original runs its economy, from static study. Every 40 ticks
(first on tick 40) the empire task runs a treaty pass for
every player, then an economy pass for every player, in index order.
Arithmetic is in doubles in the order given; ToInt is `X87.ToInt`.
R(n) is the game stream's Random(n). Confidence is high unless marked.

## Players and colonies

A player keeps a treasury, research and espionage points of the period,
the last period's income, three spending percents (research, espionage,
construction, starting at 100), improvement upkeep per resource, three
structure upkeep buckets (shipyards, kind 6 structures, all others) beside
ship upkeep, a support percent (starting at 100), the total construction
funding of its colonies, and per other player a trade ramp and receipt
and a research ramp and receipt.

A colony keeps its maximum population, last growth, five resources of the
period (credits, research, espionage, construction, and population, which
is the current population), five per-population factors (starting 10 for
credits and construction, else 0), five flat amounts, an improvement slot
for each of the 50 planetary improvements, construction funding and
points, a plan index, its structures and their upkeep.

Setting a colony resource clamps it at 0 (except espionage); population
is also capped at the maximum recomputed with the improvement bonus. A
colony whose population reaches 0 is destroyed.

Maximum population: with m = 1 + habitability, (rating + 2m)
* type + moons * m, plus ToInt(flat population) once the colony exists.
Habitability is 1 for a breathable atmosphere (or anaerobic)
plus 1 for a tolerable temperature (or subterranean).

## Treaty pass

For player P and every other player Q in index order:

- Treaty state 2 with Q: t = (Q's last income - Q's own trade receipts) *
  0.1, doubled for merchants; the ramp rises 0.005 per period to 1.0 and
  scales t while below it; P receives t. P's attitude score toward Q rises
  0.1 (clamped to 250 either way; exactly 0 becomes +1 or -1 by P's
  attitude). Without state 2 the ramp and receipt are 0.
- State 5: half the smaller of the two players' own research (each less
  its research receipts), with the same ramp and the same score rise.

Q loses nothing. No draws.

## Economy pass

With k_m = 1 + 0.1 * rate modifier m (written as 1.0 - m * -0.1):

1. Research, espionage and income of the period start at 0.
2. Each colony, front of the list first: credits = (flat + pop * factor) *
   planet score * k_finance * 0.1, added to income. Growth = pop *
   ((factor_pop + k_reproduction * 0.1) * f) * 0.1, where f is 1, 0.5 or
   0.05 by habitability 2, 1, 0; capped so pop does not pass the maximum.
3. Trade receipts join the income; the income joins the treasury.
4. Upkeep = (others + kind 6 + shipyards + ships) * 0.1. Paid in full,
   support is 100; otherwise support = treasury / upkeep * 100 and the
   treasury is 0.
5. Budget: improvement upkeep for research, espionage and
   construction is charged at the spending percents (upkeep * percent *
   0.001), and construction funding at 2 credits per unit. If the money
   runs short, funding is cut first (shared out by each colony's share),
   then the three percents are scaled down.
6. Each colony: research = pct * 0.01 * (flat + pop * factor) * (advisors +
   1) * k_research * 0.1; espionage likewise with no rate modifier or
   habitability; construction likewise times f and k_construction, plus
   funding * 0.1. Advisors are the sum of every advisor of that field's
   role and the star's governor, each with its skill for the field.
7. Espionage becomes per head: the empire figure is the total over the
   population times k_espionage; each colony's is its own per head less
   that. The empire figure pays for espionage missions and is the
   defense against them (`espionage.md`).
8. Research receipts join the research points.
9. Construction for every colony, then upkeep for every fleet.

## Colony construction

Points go to the shipyard first, then to structures, then to improvements
in the colony's plan order; what is left is lost.

- Structures: with support 100, repaired in list order. Below 100 every
  structure draws R(100) and is hit for 10 when R(100) + 1 is within
  (100 - support); a hit on a kind 7 or 8 structure draws once more. A
  structure that loses all its hit points loses its units one by one; when
  that leaves nothing standing the population shrinks in proportion.
- Repairing or building something of value bc with hit points max: the
  points buy max / bc each; what is left over is returned.
- Improvements: the first in the plan not yet complete. Completing one
  adds its per-population and flat values for its one resource (the first
  with a flat value, else the first with a per-population value) to the
  colony and its upkeep to the player.
- The shipyard builds the head of its queue (orders and refits come from
  windows and computer players).

Build cost: the tech's build cost, 80 percent for a builder
species on improvements and specials.

## Colony plans

Six plans, each the 50 improvements sorted with the original's qsort
(MsvcSort) by: 0 cost, then resource in the order 3, 1, 0, 4, 2, then
flat-valued first; 1 to 5 population, construction, research, espionage,
credits first, then cost. A new colony uses plan 0. At setup the
homeworld completes every known improvement in plan 0 order, then its
population is set to the maximum.

## Fleet upkeep

At full support each ship is repaired: a fleet repair figure from each
ship's experience and hull, shared equally, times (yard count + 4) when
the fleet sits idle at a planet of its own (or a state 3 partner's) with
a shipyard. A kind 6 structure at the fleet's planet trains ships below
level 2. Below full support every ship draws R(100) and loses (hull + 1)
* 10 structure on a hit; a ship at 0 is lost.

## Colonizing (docs/sim/movement.md)

When a fleet in colonize mode runs out of orders while circling a free
planet that is not a gas giant, its newest ship with a colonizer is used
up and the colony is founded at that colonizer's generation (its place
among the colonizers, from 1). Founding: the colony goes to the front of
the owner's list with an improvement slot for every known improvement; a
governor is created if the owner has none at that star (a portrait draw);
generation 2 adds 3 population and generation 3 adds 5 and a completed
missile base, both then complete the first improvement that gives
construction. Then every fleet is checked for supply, and first contact:
for each wormhole of the star, every other player with a colony or fleet
at the far end and no treaty with the founder gets treaty state 1 both
ways. Setup founds homeworlds through the same routine at generation 1.

## Random draws

Only with support under 100 (structures, then ships), new fleets from a
shipyard, and a governor's portrait when colonizing.

## Open

The advisor +0x40 flag, the meaning of the structure subsystem counters,
and what reads each colony's per-head espionage.
