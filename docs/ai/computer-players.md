# Computer players

How the original's computer players decide, from static study. The AI
task (`sim/time.md`) picks one computer player per run, every 20, 10 or 5
ticks by difficulty, and runs its modules. The modules keep no state
between runs: everything they use lives on the player and its colonies.
The port runs them from `OpenPax.Ai.ComputerPlayer` through
`OpenPax.Sim.IComputerPlayer`. R(n) is the game stream's Random(n).

## Order of a run

1. The build reserve G: the cost of every colony's next improvement.
2. Advisors (`sim/advisors.md`).
3. Treaties (below), given the task period, with trade packages
   answered between treaty answers and cancels (`sim/trade.md`).
4. Every colony's construction funding is set to 0.
5. Structures (below).
6. Colony ships (below).
7. Ship building (below).
8. Espionage (`sim/espionage.md`).
9. Warship fleets (below).
10. Construction funding.
11. Spending percents.

Steps 5 to 9 therefore see no funding; the economy sees what step 10 leaves.

## Judging cost

- Earning = income * 10 less research, espionage and construction
  improvement upkeep at the spending percents (percent * upkeep * 0.01).
- Free = earning less funding * 20 less all structure and ship upkeep.
- Spare(cost): Free - G - (upkeep of the ship at the head of each yard's
  queue) - cost is at least 0.
- Afford(mode, extra): the spending of a kind over earning stays under a
  limit (false when earning is not positive): defenses (other structure
  upkeep + extra) 0.2; shipyards (best yard's cost + yard upkeep) 0.05;
  training bases (likewise) 0.01; ships (queued ships + ship upkeep +
  extra) 0.74.

The units are mixed (income times 10 against upkeep the economy charges at
a tenth); kept as the original has them.

## Structures

Kinds: 5 shipyard, 6 training base (not for warriors), 7 missile base,
8 battle station, 9 fighter base, 10 probably minefield. A colony may hold
at most min(5, level of the best known tech of the kind) of a kind (no
level limit for kind 10), and each needs its weapons known: missiles for
missile bases, fighters for fighter bases, an energy, projectile or area
weapon for battle stations. An order adds an unbuilt unit to the stack
(which keeps its own tech) or a new stack at the front; construction
builds it.

1. Shipyards, while affordable and one can be placed: grow the first
   finished colony's yard below the tech level; else found one at a star
   with no yard of the player, at the colony there with the most
   construction (at least 20, later ones winning ties).
2. Defenses: at each finished colony making at least 10 construction with
   fewer defense units than half its population, the kinds that are
   allowed, affordable and below a per-colony cap (2, 3, 4 or 5 by
   difficulty) are candidates; R(4) + 7 is drawn until it names one, and
   that kind's best tech is ordered. At most one per colony per run.
3. Training bases (not warriors), while affordable: grow the one site
   below the tech level, else found it at the first finished colony with a
   shipyard. One site per empire.

Computer players never change colony plans or research allocations during
play; research picks itself (`sim/research.md`).

## Funding and spending

Above difficulty 1 with at least 500 credits: (money - 1000) / 20, as a
whole number, is shared equally between colonies with something to build
(an unfinished structure, a queued ship, or, when not at war with anyone,
an improvement left in the plan), at most 2000 each.

Spending percents: espionage 100 once the player has met someone, else 0;
construction 100; research 100 while anything can be researched, else 0.

## Colony ships

A colony fleet is one with a colonizer ship. Every run:

1. Each colony fleet circling a free planet settles it, using up a colony
   ship (`economy.md`). There is no gas giant or habitability test here;
   computer players never set the colonize mode, so they settle only this
   way, at the run after arriving.
2. Every colony fleet stops: its orders are dropped and it sits still,
   drawing a new orbit (R(3600), R(100)) each time.
3. Free planets that are not gas giants are scored max population *
   habitability * planet score and sorted best first (MsvcSort; planets
   the species cannot live on score 0 but stay in the list). First the
   planets in stars that hold a colony of the player or are one wormhole
   from one, then all planets: each in turn goes to the first idle colony
   fleet that can reach it. Left over fleets go to the player's best
   defended colony they can reach.
4. Reaching: in the same star, an order to the planet; else orders to each
   wormhole of the route (the only path in the wormhole tree) and then the
   planet, if every star on the way is supplied or within the fleet's
   jumps left of one that is. A route with a fleet of a player at war on
   it is called off and the fleet stops again (another orbit draw).

## Ship building

Colony ships come from design slot 2, warships from slots 1, 3, 4 and 5.
Ships are ordered at the colony whose first shipyard has an empty queue
and works fastest on its construction (later ones on ties).

1. With no colony ship at all (queued or in a colony fleet), one is
   ordered at once.
2. At war with anyone: warships while possible. Refits come first (types
   4 and 5 above difficulty 2, 3 above difficulty 1, then 1): a ship of an
   older design in an idle fleet circling a free yard's planet is rebuilt
   to the current design, its condition counting toward the work.
   Otherwise the type is 1, or 3 above difficulty 1, or 4 or 5 above
   difficulty 2 when the design is usable (its hull within the best
   armor's limit, and able to jump); with both 4 and 5 usable, R(100)
   chooses, before any money test. The ship must pass Afford for ships and
   leave the colony ship's upkeep spare, unless the player is rich
   (difficulty 4 or more and money above five times ship upkeep).
3. Colony ships while possible: up to the difficulty in all, and more than
   one only while some colony fleet is traveling and the money tests pass.

## Strength at a place

Used to rank colonies for left over colony ships: the colony's structure
strength (`economy.md`: hit points, shield, weapons and fighters by
condition; shipyards and training bases count 0) plus idle fleets
circling the planet, counting the player and players with a treaty level
above 3. Quirk kept: players never met (level 200) count as friends.
Structures get their weapons, shields, fighters, ECM and sensors fitted
when built (every known tech) and on learning a tech (`economy.md`):
missile bases take missiles, battle stations energy, projectile and area
weapons, fighter bases fighters, all three newer ECM, sensors and shields.

## Warship fleets

A warfleet is a fleet with ships and no colonizer. Every run:

1. Every fleet that is not a colony fleet and not retreating stops where it
   is (orders dropped, a new orbit drawn: R(3600), R(100)); empty fleets
   still in the list count too. If none did, nothing else here runs.
2. Merge: each idle warfleet with room (under 21 ships) heads for the
   first other warfleet with room it can reach safely. With two or more
   warfleets that can reach each other every one gets an order, so the
   passes below usually find nobody idle.
3. If a warfleet is idle: the colonies, then the fleets, of every player
   at war with this one (treaty level below 1, not eliminated) are rated by
   their strength there and sorted weakest first. For each target, the
   first idle warfleet whose strength is at least the player's caution
   times the target's and that can reach it (no danger test) goes for it.
   Caution starts at 1.1 and rises 0.2 with each retreat and each fleet
   lost in combat.
4. If then no warfleet has orders and no shipyard is building a warship,
   every warfleet goes for the weakest target it can reach.
5. Patrol, each pass only while some warfleet is idle: fleets with a ship
   below experience level 2 to the finished training bases (largest stack
   first); idle fleets to the finished shipyards (fastest first); idle
   fleets to the player's own colonies (weakest first). Each by a safe
   route.

Computer players never set the attack mode: a fleet arriving at a target
of a player it is at war with fights there (`sim/movement.md`,
`sim/combat.md`).

## Treaties

A computer player wants, toward each player, the highest treaty state its
attitude score passes: above -1 Neutral, 0 Trade Routes, 74 Repair and
Resupply, 99 Allied, 144 Joint Research, 174 Share Information; at -1 or
below, war (`sim/diplomacy.md`). Humans and computer players are treated
alike, except that humans get offers rarely. Every run, in player order:

1. Answers: each offer waiting is accepted when the score passes that
   state's threshold, else turned down.
2. Cancels, toward players met: for each state above the wanted one, from
   Share Information down, its own pending offer is withdrawn and a treaty
   in force is canceled; an alliance drops to Neutral and Neutral becomes
   war, so a wanted level of 0 declares war in the same pass.
3. Offers, toward players met and still in: R(1000) is drawn for each; the
   offer goes ahead always for a computer player, and for a human 10, 5 or
   2 times in 1000 by task period 20, 10 or 5. It is the lowest wanted
   state not held, at most one per player; none while one of its own is
   pending; states 1 to 3 not while an alliance is held or pending.

Trade packages (offered only by a human's trade window) are answered by
the same module: gifts always accepted, demands by relative strength, a
patience that falls 10 per demand from 30, and R(100); other packages by
their value and R(100), with a veto on giving away too many ships or
planets (`sim/trade.md`).
