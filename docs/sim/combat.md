# Combat

How the original fights battles, from static study. R(n) is the game
stream's Random(n). Battles with a human side are fought step by step on a
40 ms clock; battles with no human side are resolved all at once in one
combat step. The port does both. Confidence is high unless marked.

## When battles start

Only a fleet arriving at its target starts one (`movement.md`):

- At a fleet of a player it is hostile to (attack mode, or treaty level
  below 2): if the target circles a colonized planet, a planet battle
  there; otherwise a fleet battle. Incident 11 and the third-party
  reactions come first (`diplomacy.md`).
- At a hostile colony (computer fleets: level 1 or below and not a colony
  fleet; human fleets: level 1 or below, or attack mode): incident 10 and
  reactions, then a planet battle.

While any battle exists, strategic time stands still: each step of the
game runs one combat step of the newest battle instead of a tick. A
battle resolved at once is over in that step; several made in one tick are
handled newest first.

## Fleet battle

On a 3600 by 3600 field the attacker enters at one of eight points
around the edge (by its direction from the target) and the target sits
at the center. Fleets within 50 of the target that are not held and not
circling a planet join: first those of the same owner as either side or
allied with it, then those at Neutral, Trade Routes or Repair and
Resupply with either. Fleets at war with both, or at level 5, 6 or no
contact, stay out. Each joiner takes a free entry point by a search that
has a bug kept as is (a joiner whose own point is free is left out).

## Planet battle

A colony is defended when it has a missile base, battle station, fighter
base or minefield, or another fleet circles it. Undefended: an attacker
able to strike planets (a planet bomb or area weapon) destroys the colony
outright (war both ways, incident 7 by its population), and either way
the attacker then circles the planet. Colonies are never captured.

Defended: the planet joins with its structures (minefields lay
count * 75 * condition mines, each drawing R(3600) pairs until a point
falls 700 to 2000 from the field's far corner on free cells, so about five
pairs a mine; every other
structure that is not at 0 hit points becomes an orbital at a fixed
point), then up to four defending and four attacking fleets by treaty,
and the attacker last.

The planet's improvements under way form bands, (count - 1) / 5, each a
block ships can bomb once no enemy ship or orbital is left to shoot.

## Placement

The field is a grid of 20 unit cells. Each side's ships are placed by hull
type (2, 5, 4, 3, 1, 0) in rows of up to seven around its entry point,
turned by its facing with the whole-degree tables, each on the next free
cell; every ship, orbital and the planet disc blocks the cells under it.
Orbital sizes come from their sprites (the last visible row of the first
frame in Combat.img), which the importer records.

## Resolving at once

Rounds until the battle is over:

1. Each computer side is made to think (the same think as a battle fought
   step by step, forced, so it never rolls for retreat): with no enemy (a
   player at war) present, and unless it holds the planet or can bomb an
   enemy colony, it gives up and its ships not intercepting make for the
   way out; otherwise each idle ship picks a target: enemy
   ships, then enemy orbitals, the first taken without a score and then
   any later one scoring higher (10000 - distance + twice its strength,
   or + 50 times an orbital's count); with none, the colony's first block.
2. For each player in turn: ships leaving go once the round is past 3
   (their fleet retreats, draws a new orbit); other ships with a live
   target fire: a hit when R(100) is below attack - defense + 50, at
   least 1. Then that player's orbitals fire at the first ship of the
   lowest numbered enemy, with attack 100.
3. Damage lands in chunks of 50 and the rest (always one more roll): one
   time in twenty, R(20) == 0, on a component chosen by space (sensors,
   ECM, shield, weapons, specials; never drives or armor), otherwise on
   the hull; a ship at 0 is lost from its fleet. Orbitals take it through
   their shield onto the structure, which can lose units. Blocks take it
   from the first improvement under way, and population shrinks with what
   still stands.
4. Dead targets are dropped; shields regain some soaked damage;
   explosions age.

Attack is sensor accuracy (undamaged share) plus experience, a quarter
more for predators; defense is the average of hull, experience and drive
defense with ECM strength; damage per hit is, for weapon modes 0 to 2, the
best of three range bands of undamaged weapons (no bombs), plus fighters,
over 4.

Over: when no ship is left on the field; or, once the 35 rounds of the
last explosion are past, when fewer than two players are present. The
winner of a fleet battle always leaves (no enemy left), so it goes home
retreating; a defending planet owner stays and its ships gain experience.
Fleets that retreated raise their owner's caution by 0.2 and head for
their nearest colony in reach, else a friend's, else through a wormhole
drawn at random.

Nothing in a battle resolved at once raises incidents 7 to 9, declares
war, or rolls for retreats.

The original can loop for ever when neither side can hurt the other
(unarmed ships, all weapons damaged). The port detects that and has every
side withdraw: all ships leave the field and go home retreating
(`decisions.md`).

## Battles with a human side

These are fought step by step (`combat-step.md`), not resolved at once.
