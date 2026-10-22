# Soak runs

`run.sh --soak TICKS` plays a computer players only game until it ends or
TICKS, printing every 20000 ticks how each empire stands. Results below
are from the port as of the espionage commit, 30 stars, with no human.
None has been checked against the original yet.

A game ends when every pair of players still in has a treaty level of 4
or more (`sim/star-step.md`): Allied, Joint Research or Share
Information. Most games end that way once the losers of the wars are
gone. A year is 400 ticks.

| Seed | Players | Difficulty | Eliminated | Result |
|---|---|---|---|---|
| 77 | 4 | 3 | 1 | over at tick 188390 (471 years) |
| 1 | 4 | 3 | 1 | over at 265020 |
| 2 | 4 | 3 | 1 | over at 201400 |
| 3 | 4 | 3 | 1 | over at 251263 |
| 4 | 2 | 3 | 0 | over at 54400, the two at peace |
| 5 | 6 | 3 | 2 | over at 92063 |
| 6 | 8 | 3 | 4 | over at 219349 |
| 7 | 4 | 1 | 2 | not over at 600000 |
| 8 | 4 | 5 | 1 | over at 69990 |

What the runs show:

- Computer players build warships only at war (`computer-players.md`), so
  in peacetime they have next to no fleets and money piles up: tens of
  thousands of credits by tick 100000, and in seed 7 tens of millions.
- Games are long. At difficulty 3 a 4 player game lasts 200000 to 265000
  ticks, 500 to 660 years, 8 to 11 hours at the default speed.
- Seed 7 at difficulty 1 ends with two players at war for 500000 ticks
  and 700 battles, colonies trading back and forth (one side between 59
  and 71) and neither making peace. Whether the original does the same
  at difficulty 1 is open.
