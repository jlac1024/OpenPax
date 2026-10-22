# Research

How the original researches techs, from static study. The research task
runs every 40 ticks, after the empire task in the same tick,
and reads the research points the economy pass just produced
(`economy.md`). No draws, except the scientist bonus.

## Fields, slots and allocations

Every tech belongs to one of five research fields by its TECH class
: weapons 0 (class 6005), shielding 1 (6006, 6008), ship 2
(6001 to 6004), colonial 3 (6010), space 4 (6007, 6009). A player splits
research between the fields by allocation percents (20 each at the start;
computer players then redistribute, `players.md`).

The tech state holds a slot per field (eight, of which five are used):
the tech being researched (-1 for none), its cost, the whole points put in
and a carried fraction. A tech's cost is its research cost at
125, 100 or 75 percent for a species research level of -1, 0 or 1 in that
field.

Known: tech 0 always counts as known, -1 never. A tech can be researched
when it is not known, its four prerequisites are known, and
it is not an academy (kind 6) for a warrior species.

## Each period

For each slot with a tech, in field order:

1. Points = allocation * (research points * 10.0) * 0.01, times 0.1. The
   whole part is added to the progress; the fraction carries, and when the
   carried fraction reaches 1 it gives one more point.
2. If progress has reached the cost, the tech is learned:
   - given: it joins the known set, the known count goes up,
     its raw research cost joins the research total, and an improvement
     gets a slot in every colony that lacks one;
   - the slot is cleared;
   - the player reacts (virtual +0xC0, the same for both
     classes): structures of the colonies take the new tech when it is a
     shipyard, kind 6 or kind 10 of their kind; the best armor is found
     again; ship upkeep is summed afresh; a human fits the best armor to
     every design, and a computer player redesigns everything
     (`ships.md`);
   - a scientist species has a 1 in 10 chance (R(10) == 0) of also
     getting, free, one of the techs that need the learned one (R(n) over
     them in id order); the bonus tech is not taken out of a slot that is
     researching it, so it can be learned twice (quirk kept);
   - with automatic research (always on, unless the research window turns
     it off) the next tech for the learned tech's field is picked.

Pace: a starting homeworld makes about 12 research points a period, so
with the starting split of 20 percent a field gets about 2.4 a period
against first techs of 750 to 1000: some 300 to 400 periods (40 to 50
game years). Putting everything in one field cuts that to about 80.

## Picking research

Among techs of the field that can be researched and are not in a slot:

- Humans, and computer players below difficulty 3: the cheapest, the
  first of equal cost.
- Computer players at difficulty 3 and up, the first of these that finds
  one: for shielding and ship, the first of techs 39, 42, 43, 46, 50, 51,
  186, 187, 188 in id order; except for colonial, the most
  expensive, unless it costs more than half the field's total
  ; the cheapest, with colonial ties going to the better
  resource in the order research, construction, credits, espionage,
  population; then the plain cheapest.

A picked tech goes in the field's slot with its cost and any progress
saved for it (only the research window saves progress, when research is
changed by hand).

When a field has nothing left to research its allocation is
set to 0 and handed out one point at a time to the other fields with
research under way; when anything at all can still be researched, a
fighter tech is put in the space slot first if one is waiting
.

At player creation the free techs are given and the first
research of every field is picked the same way.

## Orders from the Research window

The window (`ui/research.md`) gives three orders: a tech for a field
(with its kept progress), stopping a field (its progress kept by tech),
and a field's share of the points, held so the five never pass 100.
