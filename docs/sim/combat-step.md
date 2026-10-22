# The battle fought step by step

Battles with a human side are fought in steps of 40 ms in the original
(combat step, tactical step), and so they are in the
port: the field, orders, formations and movement; sensors, weapons, shots
and damage; the planet, its orbitals, bombardment and effects; fighters and
mines; and how a side thinks. The player watches and commands in the battle
window (`docs/ui/battle-window.md`). Battles between computer players alone
are resolved at once (`combat.md`); `Game.FightStepByStep` can turn the step
battle off, which some tests use.

Code: `Game.CombatStep.cs` (the step and its end), `Game.CombatMotion.cs`
(movement), `Game.CombatOrders.cs` and `Game.CombatFormations.cs`
(orders), `Game.CombatWeapons.cs` (sensors, fire, shots, hits),
`Game.CombatPlanet.cs` (blocks under bombs), `Game.CombatOrbitals.cs`,
`Game.CombatEffects.cs`, `Game.CombatFighters.cs`, `Game.CombatMines.cs`,
`Game.CombatThinking.cs`, `Combat/`.

## The field

The same 3600 by 3600 field, placement and grid as a battle resolved at
once. Every object has an id from one counter for all battles (0 is never
used), an exact position in doubles with a rounded copy, a speed and a
heading in whole degrees. The grid of 20 unit cells holds 0 for free, -1
for covered and, in a footprint's corner cell, the id of what stands there.
Marks are not counted: clearing a footprint also clears the cells of
anything overlapping it, as in the original.

A ship also has its top speed (its combat speed), a turn rate of 11.25
degrees a step for the two smallest hulls, half that for the middle two and
a third for the largest, its sensor range and targets tracked (by the
sensor's condition), the heading it was told, a goal and a detour, a count
of steps held up, and whether it launches fighters by itself.

## Orders

An order names the battle, a target and an orbital by id, a point, and the
chosen ships. The ships are read back as the original reads its order
message: those still in the battle, each put first, so they are taken in the
reverse of the order given.

| Kind | Each ship |
|---|---|
| Face | turns to face the point |
| Target | faces the target and fires at it without moving |
| Attack | chases the target to range |
| Fighters at / defend, recall one | (with fighters) |
| Auto launch | turned over |
| Retreat | leaves by the safest way |
| Move, lines, diagonals | goes to its slot |
| Horizontal with no ships | AUTO for the side of the ship named |

A group move puts the ships in rows of five across the way they travel,
the nearest ship in front; a quirk kept turns each slot's sideways offset
with the already rounded forward one, so rows come out skewed. Lines put
ships 100 apart, diagonals 83 apart each way, kept off the edges (a line
across may reach 3650, a quirk kept). Slots and ships are sorted alike so
each ship takes the slot that matches it; nothing holds a formation after.

The retreat picks the way out with the least threat: each enemy ship and
defending orbital weighs on the 30 degree sector it lies in and less on
those beside it, a planet toward the far corner; ties go to the nearer
exit.

AUTO makes the side fight by itself and think at once; turning it off
leaves the side's ships idle (still holding their targets). It works only
while the side has a ship on the field.

## Movement

A ship turns on the spot by its turn rate, then moves its full speed along
its heading: no acceleration. Quirk kept: the turn is floored, so a ship
turns one degree more to the left than to the right. A ship chasing a
target keeps up to 10 degrees of error rather than stop to turn, and stops
260 from its goal (300 when standing, 300 less its size against a planet
it can bomb), facing the target. Quirk kept: a heading that rounds to 360
is never reached.

A ship whose way is taken waits; after four steps held up it plans a way
round: straight on if three points toward its goal are free, else the first
free heading fanned out 5 degrees at a time within 70 degrees, else straight
ahead, else within 180 degrees, each needing four free points along it. It
replans after every step on a detour. When its goal itself is taken it gives
up once within its size of it; otherwise it can go on looking round for
ever, as in the original.

A leaving ship heads for its exit and goes once within 1.5 times its size
of the edge: its fleet retreats as when a battle is resolved at once.
Every hundred steps of leaving, across all battles, the ship then moving
picks its exit again.

## Sensors

Every 10 sub-steps each ship's sensors pick up the nearest enemy ship
(else orbital) it does not yet follow, when within sensor range: one a
look, up to the targets its sensor tracks. Quirk kept: the limit is tested
for equality, so a sensor damaged below the count keeps adding. Enemies
dead or out of range are dropped each step.

## Firing

Every 5 sub-steps. Each weapon stack fires in its arc: bow within 30
degrees of the heading, port and starboard to either side, point defense all
round. An arc takes the ordered target when it lies in it, else the nearest
followed enemy in it within 1500; point defense takes the missile it wants
most (nearer, coming for this ship, fewer shots already at it), else the
nearest followed enemy within 350. A weapon fires when its target is within
its reach (the last range band with damage: 300, 500, 750 or 1500).

A stack fires size / 14 rounds a phase (2 to 6 by hull; point defense 3)
until its volley is spent, then reloads for its reload time (half for point
defense); the next volley is its working count. One shot carries the
rounds. Firing draws nothing.

## Shots

Beams, shells and bombs fly at full speed on a heading aimed ahead of a
moving target (quirk kept: two steps past where they would meet) and never
turn. Missiles start slow, gain 0.5 a step to their top speed and turn 5
degrees a step toward their target (6 to the left, as ships). Damage per
round is the weapon's band for the distance flown, times its condition,
truncated; a shot whose damage falls below 1 is gone (quirk kept: a
weapon too weak for a point dies at once).

A shot that reaches its target rolls 100 for each round: beams against the
shooter's accuracy and the weapon's, less the hull's defense, plus 50;
missiles against their tracking less the target's ECM, plus 100; never
below 5. All missed: the shot flies on without a target (a missile on a
heading turned at random) and dies at its goal. Some hit: they land in the
hits phase, the rest fly on. Point defense shots that reach a missile strip
its hit points, a missile of the salvo going each time they run out.

Reaching something of a player not at war with the shooter declares war
both ways; the side shot at weighs giving up afresh and thinks again at
once (ships already leaving keep going).

## Damage

Each round that landed in turn: a beam is cut by the ship's shield
(percent first, truncated, then soaked up to its capacity; missiles pass
through shields). What remains, if any, marks the last damaging hit. A roll
of 1000 at or under 30 divided by condition, armor class and armor decides
whether a component takes the hit first; what it cannot take goes on to
the hull. A ship whose structure falls to 0 dies: its shooter's side is
credited a tenth of its full structure, its owner grows more cautious if it
was its fleet's last ship, and the shooter gains an incident. At the end
each ship gains its side's credits shared out, plus one.

Shields regain what their regeneration allows each step.

## The planet and its blocks

The planet is an object of the battle too. It never moves, fires or takes
hits, and stands at (0, 0) until a block falls. Its blocks, one per band of
five improvements under way (up to 10), stand in a fixed layout round it in
the field's far corner, each shifted by half its picture's width and by its
picture's height (Combat.img pictures 20100 on for planets of class 4,
20200 on for the rest), so the importer records every picture's size.

A ship with a ready weapon that bombs (weapon type not 0) aims at the
highest standing block within 1500 that is not its own side's (quirk kept:
no war test; the war comes when the bomb lands). Such a weapon fires at the
block after any shot at a ship, so an area weapon can do both in one phase.
At a block the whole volley goes as one round of that many weapons' power,
which reads the longer range table once it passes one weapon at full
condition.

A shot lands on a block within 15 of its center, with no roll (quirk kept:
a fast shot can step over it and fly on). In the hits phase, after
everything else, each bomb lands whole: the colony takes damage per round
times rounds (the first improvement under way takes all of it, and
population shrinks with what still stands), and the shooter's player an
incident for the population lost. The struck block falls when the colony
has fewer bands than its index, and the planet object moves there to carry
its ruin; otherwise the block shows a split. The other blocks above the
bands left fall in the post-step, with no ruin. A colony destroyed takes the
mines with it. Blocks draw nothing.

## Orbitals

Only missile bases, battle stations and fighter bases look and fire. Every
10 sub-steps each takes one more enemy ship to follow, up to its sensor's
targets: the one nearest the field's far corner (3600, 3600), anywhere on
the field (quirk kept: from the corner, not from the orbital). With no
sensor it follows nothing and fires its weapons only at an ordered target.

Every 5 sub-steps it fires. Each shot carries one round per 25 percent of
the stack left (at least one), at accuracy 100 and power 150, so damage is
half again the weapon's and reaches 2000. The second energy weapon is point
defense: the missile it wants most within 320 (one coming at it most of
all). The two weapons fire at the ordered target within 1500, else at the
followed ship scoring best: nearer the corner is better, one not just shot
at better still, and none more than 2500 from the corner. No arc, range or
war test. A volley is one shot more than the stack holds (three times for
point defense), then the weapon reloads for half its time.

Hits on an orbital roll 100 a round with no defense. In the hits phase,
after ships, beams go through its shield, and each round's damage falls on
the stack whole (a missile base or battle station draws for damage to one
of its parts each time). A stack destroyed credits its shooter by its size,
gives the shooter's player an incident, explodes and frees its footprint.

Orders to an orbital by the order's kind: 3 and 4 set its target, 5 sends
its fighters (with fighters), 7 turns its launching by itself over.

## Effects

Explosions and flashes are objects too, taking ids. They show only, but a
battle cannot end while one is still dying down, so their lifetimes count:
35 steps, except shield flashes (10) and one kind of 39. One riding on an
object (a hit flash, a shield flash, a block's split or ruin) keeps to it
and goes at the next move once it is dead; the rest drift on as what made
them moved. Where the original picks among three explosion pictures with
its clock mixed generator, the port takes the first.

## Fighters

A fighter is a flight of three craft, each with a third of the fighter's
hit points. A carrier brings one flight per working fighter bay unit, made
before the ship is placed, so they wait docked at (0, 0) (quirk kept); a
fighter base brings count times two by what is left of it, plus one, of
each fighter fitted, at the base. Neither takes part in a battle resolved
at once.

A ship launches on its fighter order or by itself, every fifth fire phase
once something it follows is within 5000: two docked fighters, one out
each side, else one flying fighter turned onto the target. A ship's fighter
flies straight out for 24 moves, then on the ship's heading for 25, then
aims for itself; a base's aims at once and goes one at a time. The same
order at a friend sends one to guard it. A recall brings one home; it
docks within 20 of its carrier, repaired but short the craft it lost.

Out, a fighter flies round its target on a circle of 225, point to point
in steps of 22.5 degrees, at its speed, over everything; one point in
five (or near the field's edge) it crosses to the far side and turns the
other way. Every new point, and every launch, draws R(5). Quirk kept: the
circle's index wraps by 15, skipping its first point. It fires each weapon
from within 300, one shot per craft at the fighter's accuracy and half
power; a reload counts down two a phase, only while it has a target. A
guard fires at the enemy nearest its friend. A fighter whose target dies
takes the nearest enemy ship, orbital or fighter anywhere, else goes home;
with its carrier gone it flies on, and dies once nothing is left to chase.
Quirk kept: such an orphan never fires.

Shots reach a fighter within 20 and roll 100 a round (beams against its
defense, missiles against its missile defense); all missed, the shot
flies on and may roll again; however many hit, one round lands, whole. A
craft lost credits its shooter a tenth of its hit points; damage a
carrier's fighters take while it lives wears its fighter bay, which is
how losses outlast the battle. A carrier destroyed or leaving takes its
docked fighters with it; those out lose their carrier.

## Mines

A minefield lays its mines when the battle starts (also one resolved at
once, where they only lie there): count times 75 by what is left of it,
each at random in a ring 700 to 2000 from the field's far corner on free
cells, drawing again until one fits. A mine has the minefield's strength
as damage and hit points, and its level as sensor range (the first
minefield's mines never move). It creeps at 1.25 a step toward the
nearest enemy ship in range and home again when it loses it (quirk kept:
a nearer ship found ends its look at that player's ships). A ship that
touches an enemy mine takes its damage straight off its structure, with no
shield, credit or incident. Point defense shoots at mines its side can see
(its own, a player not at war, or with a minesweeper of the mines' level or
better; quirk kept: the newest mine's level stands for all of a player's);
a shot within 20 lands with no roll. A mine shot down credits its shooter
3 and marks the last hit even when it did no damage (quirk kept).

Each mine lost costs its minefield a seventy-fifth of its tech's hit
points and one mine out; the minefield goes once none is out. In a
minefield above the first level a resting mine moves up to fill the gap. A
computer ship under way stops when it sees an enemy mine within 240. A
colony destroyed takes every mine with it.

## Thinking

A side fights by itself when it is a computer player, or a human side with
AUTO on. Each side thinks once every 20 steps, in the sub-step of its
number. A computer side does nothing on its first seven thinks (its ships
still fire at what they see); AUTO and a war declared on fire make a side
think at once, past that wait.

When it acts, a computer side first weighs giving up: with no enemy ship
or orbital left, unless it holds the planet or can bomb an enemy colony,
every ship not intercepting makes for the way out and the side never
thinks again (unless shot at by someone new). A human side never gives up.

Then, on every other think, each ship of a computer side that is not the
planet's owner may break off. The side compares what is left of its first
ship's strength since the battle began with the same for its enemies, in
thousandths: threshold = own * 100 / enemies + 40 (+140 for a warrior
species, which never breaks off). A ship adds its experience (-30 at the
lowest level to +100 at the highest); at 100 or more it holds with no
roll, else it breaks off when R(100) beats it. Quirks kept: an enemy that
never thinks always looks whole, and a side with no strength of its own at
the start counts its share as 1. The original crashes when the enemies are
worn below a thousandth; the port counts them as one (decisions.md).

Last, each idle ship takes the best target as when resolving at once
(colony ships only face it; the rest chase it). No formations, group
moves, or orders to fighters or orbitals: those go by themselves. Thinking
draws only the retreat rolls.

## Each step

1. A side thinks (sub-steps 0 to 15, side by sub-step).
2. Sensors look, every 10 sub-steps: each player's ships, orbitals, mines.
3. Shots, missiles, ships, mines and fighters move, then effects.
4. Weapons fire, every 5 sub-steps, each player's ships, orbitals and
   fighters; then hits land on ships (then the mines they touch), orbitals,
   missiles, mines, fighters and blocks, in that order.
5. Post-steps, player by player: a dead target is dropped; a goal following
   something dead goes and the ship stops; a dead ship goes after three;
   orbitals drop dead ships; mines let go of dead ships; fighters head home
   when their ship leaves, and decide again; blocks above the bands left
   fall. Then effects age.
6. The sub-step goes on (20 in a cycle), then the end is checked.

A battle ends when it has been quiet: with any ship able to move, 1500 steps
after the last damaging hit and once no ship is moving (every ship gains
experience and its fleet is let go, still retreating if it was); with no
ship able to move, 250 steps after the last shot once every fighter is
docked. Otherwise it ends as a
battle resolved at once does, when fewer than two sides are left, once no
effect is left; the colony's bands are counted afresh for its owner, and
fighters, docked or out, count for their side (mines do not).
