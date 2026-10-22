# Espionage

Each player keeps a list of missions (the EspionageMan module, +0x70C).
A mission (0x30 bytes) names its owner, its target, one of
nine kinds and a level from 0 to 2. Missions get numbers from one game
counter (kept in saved games; taken as starting at 0).

## Kinds

| Kind | What it does | Cost | Chance |
|---|---|---|---|
| 0 | steal ships | 50 | 0.15 |
| 1 | destroy ships | 25 | 0.2 |
| 2 | sabotage a colony | 25 | 0.2 |
| 3 | locate a colony | 50 | 0.15 |
| 4 | turn field advisors | 50 | 0.1 |
| 5 | turn governors | 20 | 0.1 |
| 6 | assassinate field advisors | 30 | 0.15 |
| 7 | reveal systems | 20 | 0.25 |
| 8 | steal techs | 30 | 0.2 |

| Level | Cost times | Chance times |
|---|---|---|
| 0 | 1 | 1 |
| 1 | 2 | 1.5 |
| 2 | 4 | 2 |

Cost is in espionage points: kind cost times the level's. The tables sit
(kinds) and (levels) as (cost, chance) pairs.

## Paying (every 40 ticks)

The espionage task visits every player, eliminated ones too
(their lists are empty). The period's espionage per head (+0x720, see
`economy.md`) goes into the missions in list order while any
is left: a mission takes what it still owes, or everything if that is
less. When it has its cost it is tried and removed. Quirk kept: a mission
paid off with points to spare is credited its whole cost, not what it
owed; the leftover is still right.

## Trying a mission

- chance = level chance * kind chance, doubled for a Spy culture owner;
- defense = target espionage per head * 10, doubled for a Psychic target.

1. r = R(100) + 1. If r <= chance * 100 the spy gets in:
   - r = R(100) + 1. If defense < r the mission works (below). When it hit
     anything and is not kind 4, 5, 7 or 8, one more R(100) + 1 decides
     whether the target's notice names the spy (it does when the roll is
     at most the defense).
   - Otherwise the spy is caught; the target is told, with no incident.
2. Otherwise r = R(100) + 1. If r <= defense the spy is caught: the
   target is told and it is incident 6 (`diplomacy.md`). Else it failed
   unseen.

Every outcome sends the owner a report; these, and the target's notices,
are messages only (player virtuals +0x8C to +0x98). The port raises
`EspionageReport` when the local player is told something.

A cheat tries every mission at once with both rolls passed;
the rolls are still drawn. It is ported as `Cheat.EspionageSucceeded`,
reached from the developer console (`docs/ui/console.md`).

## What each kind does

Rolls are R(100) + 1 against a bound, so "10%" means r < 11.

- **0, steal ships**: each ship of each target fleet, in
  order, at 10%, leaves its fleet. For each, the owner makes a new fleet
  named STR# 112 entry 6 ("Stolen Ship Fleet") at its newest colony's
  planet, or the first planet of the first system. A human owner gets the
  ship; quirk kept, a computer player's ship is simply deleted and the
  empty fleet is dropped by the next star step.
- **1, destroy ships**: each ship at 10% is destroyed.
- **2, sabotage**: picks a colony (below), then each structure
  stack at 50% loses one unit, then each improvement with a slot there,
  built or not, at 50% loses 99999999 construction (which takes
  a finished improvement's effect away).
- **3, locate a colony**: picks a colony and names it in the
  report. Nothing else.
- **4, turn field advisors**: each field advisor not already
  the owner's, at 25%, works for the owner from now on (`advisors.md`).
- **5, turn governors**: the same for governors at 15%.
- **6, assassinate**: each field advisor at 25% is removed.
  Nobody replaces it until the field is filled again.
- **7, reveal systems**: each system the owner has never seen
  and the target has, at 50%, gets a sight update with the owner shown it
  (`visibility.md`).
- **8, steal techs**: each tech id 0 to 899 the target knows
  and the owner does not, at 15%, is learned. If one of the
  owner's research fields was working on it, the field stops, and quirk
  kept, the learned effects (AI redesigns included) run a second time;
  with auto research the field picks again.

Picking a colony walks the target's colonies newest first, round and
round, taking each at 15%, until one is taken.

## Computer players

Run eighth in a computer player's turn (`ai/computer-players.md`), and
only when its espionage per head times 10 is above 0 and it has no
mission. It then repeats until a mission starts or no target is left:

1. A target: if any other player still in the game has a
   treaty level with it below 4 (met, not allied), draw R(player count)
   until one such is drawn.
2. level = R(3), then kind = R(9). The mission starts when:
   - 0: the target has fleets and the level is war;
   - 1: fleets, level at most neutral;
   - 2 and 3: colonies, at most neutral;
   - 4: a field advisor not already turned to it, at most resupply;
   - 5: a governor likewise, at most resupply;
   - 6: a field advisor not already turned to it, at most neutral;
   - 7: never;
   - 8: at most resupply.

## Elimination

When a player is eliminated, every other player's missions against it
go, and so do its own.
