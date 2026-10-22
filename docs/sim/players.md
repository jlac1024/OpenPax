# Players, homeworlds and starting assets

What the original does at a new game besides building the galaxy
(`universe.md`), from static study of the original program. R(n) is the game
stream's Random(n). Confidence is high except where noted.

## Order

1. Game setup: store seed and difficulty, Seed(seed).
2. AI player choices, then player objects.
3. Relations.
4. Universe, which reseeds: the galaxy reuses the stream the
   steps above already drew from.
5. Per player in index order: home star, home planet, homeworld changes,
   then all of that player's starting assets.

## 1. AI player choices

AIs = setup+0xC - setup+0x10 + 1. Taken flags[30], fleets[16], names[30]
start clear; human flags and fleets are marked (human names are not). Per
AI in order: species R(8) (index into the RACE list, repeats allowed);
fleet R(16); flag R(30); name R(30) redrawn while taken (STR# 46000);
then flag redrawn with R(30) while taken; then fleet redrawn with R(16)
while taken. Taken ids carry over to later AIs.

## 2. Player objects (humans first)

Per player, in order:

1. **Advisors**: four, k = 0..3, each drawing
   portrait = (species picture + 1) * 1000 + R(10), then
   slot k = (R(5) + 8) * 0.01 for politicians, else (R(5) + 6) * 0.01.
   8 draws per player. Money 5000. Research allocations 20 each; every
   tech with research cost 0 is known (prerequisites ignored). Best armor
   remembered.
2. **Human**: three designs of types 0..2 (no draws).
3. **AI**: techs 1, 2, 3 given if not known; each given one rebuilds all
   designs (2 draws: R(100) for the type 1 and type 3 designs). With GOG
   data tech 1 is already known, so 4 draws.
4. **AI research split**: W[5] = 10, total 50. With the
   species research levels L0..L4, thresholds T0 = L0 + 2,
   Tc = T(c-1) + Lc + 2. While total < 100: r = R(T4) + 1; the first c
   with r <= Tc; amount R(7) + 1 if c's trait test holds (c0 warrior or
   predator, c1 warrior, c2 nomad, c3 builder or spy, c4 builder), else
   R(3) + 1; capped so total does not pass 100. Then each W[c] is set
   through a clamp against the other allocations, which still hold 20, so
   weapons start at 20 at most and the total can stay under 100 (quirk
   kept).

## 3. Relations

Pass 1, per player p and slot j = 0..15: no draw for
missing players or p itself (value 0). Otherwise R(6) + 1 maps to
-3, -2, -1, 1, 2, 3; plus or minus half the difficulty (AI with AI versus
otherwise); trait adjustments of p (warrior -2, nomad -1, predator -3,
scientist +3, politician +2, merchant +1); 0 becomes +1 (AI with AI) or
-1; clamped to -3..3.
Pass 2: per p and other existing j: rel[j] + R(40) + 10.

## 4. Home star

m = N / players (integer), at least 2.0. Up to 10 rounds of 10 candidates
R(N), redrawn while already a home; accepted when the wormhole hop count
to every earlier home is at least m (as doubles); after each rejection
m = (m - 2.0) * 0.1, at least 2.0. Then 10 candidates needing 2 hops;
then any free star. Hops come from a BFS over wormholes (always reachable,
the network is a tree).

## 5. Home planet and homeworld

First planet in orbit order minimizing |T - preferred + tolerance| (the
"+ tolerance" looks like a bug; kept). Then: type 3, size 40,
rating 5, 2 moons, one more orbit step, class from the species (anaerobic:
5 becomes 3; else the first breathable atmosphere 0..3; else unchanged),
temperature = preferred, score 11 for an AI or 11 - 2 * (difficulty - 1)
for a human. No draws.

## 6. Starting assets

1. AI: rebuild all designs: R(100) for type 1 and type 3 (a minesweeper
   is added when R(100) + 1 <= 30).
2. Colony on the homeworld: 1 draw (governor portrait R(10)).
3. Two structures: best known kind 5 (shipyard) and kind 7 (missile base).
4. Fleets, each drawing R(3600) and R(100) for its orbit (radius 30,
   speed 50/30): an AI gets difficulty-many fleets of one type-2 ship; a
   human gets a fleet of 2, 2, 1, 1, 0 type-2 ships by difficulty 1..5
   (empty at 5, still drawn) and a fleet of one type-0 ship.
5. The colony is founded through the same routine as colonizing
   (`economy.md`), at generation 1: improvement slots, governor, supply
   and first contact. Then both structures and every known improvement
   are finished at once, in plan 0 order; the structure decay path with
   R(100) only runs when support is below 100, which never happens at
   setup.
6. Population starts at the colony's maximum, which the population
   improvements may have raised.

Per player draws after the universe: home star draws, then AI
2 + 1 + 2 * difficulty, human 1 + 4.

Object ids: fleets take ids from the universe counter (after the
wormholes); advisors, designs and ships use a second counter.

## Design builder

Used for every design; component choice is in `ships.md`. It draws only
for AI types 1 and 3. A human's first starting fleet holds colony ships
(the type 2 design in slot 2), the second the type 0 design.

## Port

`OpenPax.Sim.Setup.NewGame` runs all of the above in order with every
draw. Tests check a lone human's draw count exactly. Not yet modeled,
and none of these change draws:

- where the fleet name counter starts (taken as 1) and whether the AI's
  blank starting designs take secondary ids (taken as no).

Star visibility is set at setup as in play, by the colony and fleet
updates (`visibility.md`); it makes no draws.

It does run at setup: the app's game-running byte is set before the game
is made (`ui/new-game.md`).
