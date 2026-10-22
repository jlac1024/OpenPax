# Game clock and turn loop

How the original advances time in a running game, from static study of
the original program. There are no turns: the game runs in ticks, and periodic
work hangs off a scheduler. Confidence is high for the clock, scheduler,
task list and calendar; the purpose of some tasks is only known from the
module they call into (link order), as marked.

## Clock ( setup run)

The clock is polled from the main loop. It keeps two timers, one for the
strategic game and one for combat.

- Strategic: when at least the interval has passed since the last step,
  the last-step time becomes now and one tick runs. Only one tick runs per
  poll, so a slow frame loses time instead of catching up. A "step now"
  flag (+0x28) forces a tick without resetting the timer.
- Combat: the same with its own timer, a 40 ms interval and its own flag
  (+0x2A); a combat step runs the combat object instead of the scheduler.
  Which timer is used is a mode flag (+0x29). A pause flag (+0x2B) stops
  both.
- Interval by game speed: 0 = 600 ms, 1 and 2 = 450, 3 = 150,
  4 = 75, 5 = 38, 6 = 13. Other values leave it unchanged. The speed starts
  at 3, the default of the `GameSpeed` preference.
- Multiplayer only: ticks run in groups (1 per sync in single player, 3 at
  the start of a networked game) and the clock waits for the network sync
  between groups. The port does not keep this (no legacy netplay).

No elapsed time is passed into a tick.

## Scheduler

A singly linked list of tasks on the game object (+0x9CA0). Each task has a
period and a countdown. A tick sets a flag on the game (+0x9C94, use not yet
known), then walks the list in order: countdown minus one; at zero the task
runs, and the countdown goes back to the period, or the task is removed if
it returned false. Every current task returns true.

Adding a task puts it at the front of the list and adds a
delay to its countdown; all tasks are added with no delay, so a task of
period P first runs on tick P and then every P ticks.

## Tasks

Added in this order, so they run in the reverse order each tick: the
autosave first, the star step last.

| Added | Runs | Period | Method | What it does |
|---|---|---|---|---|
| 1 | 10th | 1 | | Every star system: planets' step (virtual +0x40), fleets (each one with no ships left is removed; one whose step returns false leaves the system), then a third object list |
| 2 | 9th | 20 / 10 / 5 | | One computer player thinks (below). Period by difficulty: 1 gives 20, 2 gives 10, 3 to 5 give 5 |
| 3 | 8th | 4 | | Calendar (below) |
| 4 | 7th | 200 | | Every player records ten statistics for the comparison graphs (`history.md`); then the local player's graph window redraws |
| 5 | 6th | 2000 | | Per player, each governor may grow one skill (`advisors.md`) |
| 6 | 5th | 200 | | Per ordered pair of players with a relation other than 200: compares average strength of fleets and colonies and adjusts attitudes (kinds 3 and 2) |
| 7 | 4th | 40 | | Per player on +0x704 (ResearchMan module) |
| 8 | 3rd | 40 | | Per player on +0x70C (EspionageMan module): espionage pays for missions (`espionage.md`) |
| 9 | 2nd | 40 | | Two passes over the players through the empire object:, then (Empire module) |
| 10 | 1st | 4000 | | Autosave to `PaxAutoSaveGame` if the `AutoSaveGame` preference is on. Single player only |

Players are visited in index order (game +0x9C90 is the count
gets one).

## Computer player task

The task keeps a player cursor (+0x10), starting at 0. If any player is a
computer player (class 7102) whose flag at +0x4F0 is clear
(presumed "out of the game"), it walks from the cursor, wrapping, to the
next such player, leaving the cursor just past it. That one player's AI
modules then run, one call each, in a fixed order (research, ship design,
fleets, colonies, diplomacy and the rest; ported with the AI). So with k
computer players each one thinks every k runs of the task.

## Calendar

Two fields: a year starting at 2100 and a step byte. Each run (every 4
ticks) adds one to the step; at 100 it goes back to 0 and the year goes up
by one. A year is 400 ticks, 60 seconds at the default speed.

## Speed 0 as a pause (the port's own)

With `GameClock.ZeroPauses` (the SpeedZeroPauses setting, on by default,
decisions.md) speed 0 holds the game: no strategic step and no combat
step, as the pause does; a forced step still runs. The speed notice then
reads the original's "Game Paused" (list 75, 17) instead of "Speed set
to 0". With it off, speed 0 is the original's 600 ms a step.
