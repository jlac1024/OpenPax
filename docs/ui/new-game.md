# New game screens

The single player flow from Game Start to the first game screen. Every
full screen is a mode (switch), rebuilt on each visit, so no
screen keeps state between visits. Code: `src/OpenPax.Ui/Screens/`.

| Mode | VIEW | Palette | Background | Screen |
|---|---|---|---|---|
| 1 | 31000 | 1 | 31000 | Game Start (a second variant, VIEW 31001 palette 15, is picked by the clock; the port shows the first) |
| 8 | 11000 | 3 | 11000 | Species Selection |
| 9 | 11300 | 4 | 25000 | Player Design |
| 10 | 10000 | 5 | 10000 | Game Setup |
| 11 | 22000 | 0 | 22000 | Main control screen, the bridge |

## Game Start

New Game (31105) opens Species Selection. No movie and no draw.

## Species Selection

The eight built-in species (RACE records 1 to 8 in order) stand in a
lineup. Each has a hidden hot spot (11010 to 11017), a small figure
animation (11020 + slot, pictures 11100 + 20 * slot) and a large one
(11030 + slot, 11260 + 20 * slot), 15 frames at 30 fps. Picking a slot
disables its hot spot and plays the small figure; when it ends the large
one opens, and then the name (11100) and description (11101) show. The
arrows step through the slots, wrapping. Stock and Custom latch; Custom is
enabled only when custom species exist.

Accept keeps the species for Game Setup (app +0x80). Design opens the
designer with it as the template (app +0x84). Cancel goes back to Game
Start.

Custom shows the designed species one at a time with one generic figure
(11038); the arrows step through them with no figure, Delete removes the
one shown for good (back to the lineup when none is left). The designed
species are kept in the per-user folder as `SpecData`, in the original's
`Data\SpecData` layout ("II", then records of 46 bytes plus the name and
description), so an old file can be copied in. At most 100 are loaded
(the original overruns past 100).

## Player Design (VIEW 11300)

The designer starts from the species shown in Species Selection, named
"Unnamed". Code: `src/OpenPax.Ui/Screens/PlayerDesignScreen.cs`; study
note build/scratch-ui/player-design.md.

- Points, budget 10 (`RaceTemplate.Points`): extra atmospheres 1 each
  (none for an anaerobic species), anaerobic and subterranean 5, psychic
  3, predator and aquatic 2, each cultural trait 2, and every rate and
  research level at face value (a -5 rate gives 5 back). Points Spent and
  Points Remaining show plain numbers; ACCEPT works only within the budget
  with a name and a description.
- Unticking the last atmosphere ticks its partner (carbon dioxide with
  methane and ammonia, oxygen and nitrogen with sulfur dioxide).
  Anaerobic hides the atmospheres and their cost row; subterranean hides
  the temperature buttons and marker. The values stay and come back.
- 18 portraits (Advisors.img), wrapping; the temperature marker moves
  with the temperature chosen. Temperature tolerance has no control and
  keeps the template's (always 60).
- CLEAR gives the defaults (carbon dioxide only, very hot, nothing else,
  0 points). Hover help shows after 350 ms, counted from when help last
  showed (a quirk kept).
- ACCEPT keeps the species with the designed ones (quirk kept: every
  Accept adds another copy) and goes to Game Setup with it; CANCEL goes
  back to Species Selection with nothing kept.

## Game Setup

| Id | What | Range, default |
|---|---|---|
| 10001 | galaxy preview | |
| 10002 | number of stars | 20 to 100, 20 |
| 10003 | computer opponents | 3 to 15, 15 |
| 10004 | difficulty | 1 to 5, 3 |
| 10005 | empire name | up to 15 characters, STR# 10000/8 "Your Name" |
| 10006 | fleet color | 0 to 15, with arrows 10103 and 10104 |
| 10007 | flag | 0 to 29, with arrows 10105 and 10106 |
| 10100 / 10101 / 10102 | Start, Cancel (back to Species Selection), New Universe | |

The original remembers the three spinners, the name, flag and fleet
color in its preferences; the port does not yet.

The preview runs the universe's first two steps for 100 stars
(positions, then types after a reseed) and shows the first ones, so it
shows the real galaxy: Universe.img picture 1 behind, each star Universe.img
2000 + type centered at its position scaled from the 450 square to the
panel. The default seed is a clock value below 25000. New Universe takes
R(10000) from the stream the preview just used, so the next seed follows
from the old one alone. None of these draws reach the game, which reseeds.

The fleet panel shows six ships of the color from Combat.img (picture
(i + color * 10) * 100 + 1001) over Universe.img layers 1 and 8 scrolling
left. The flag panel shows the flag large (Empire.img 3000 + flag) between
three small neighbors on each side (1000 + flag), wrapping.

## Start

The setup record gets the seed, star count, opponents, one human,
difficulty, and the human's name, flag, fleet color and species. Then the
game is made: the app's game-running byte (+0x5C) is set
first, so star visibility and the other checks on that byte run during
setup too, the random call counter is reset, the game is set up
(`sim/universe.md`, `sim/players.md`), the tasks and clock are made, and
the bridge opens. No movie plays.
