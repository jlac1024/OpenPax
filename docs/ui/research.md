# Research window

Mode 13, VIEW 18000, palette 2 (ctor, handler),
reached by F3, the bridge's Research station and research notices. Code:
`src/OpenPax.Ui/Screens/ResearchScreen.cs`; the game side in
`src/OpenPax.Sim/Game.ResearchOrders.cs`.

## What it shows

- Five field tabs (STR# 18000: WEAPONS, SHIELDING, SHIP, COLONIAL, SPACE).
  A tech's field comes from its class (`sim/research.md`).
- A list of the chosen field's techs: THEORETICAL lists those that can be
  researched now (unknown, prerequisites known, no academy for a warrior
  species; those already a project included), DEVELOPED the known ones.
  Techs still locked are never listed; there is no tree view. Columns:
  name, research line (STR# 105) and the cost for this player, or
  "Discovered". Sorted by the chosen heading (name, line or cost, ties by
  name). A project's row shows in its own color (55, or 16 when picked;
  others 26, or 14 when picked). Rows are 18 px, so 8 show.
- The shown tech: its picture turning (Tech.img, 36 frames at 66 ms), its
  name, and, for techs other than ship components and installations, its
  description (STR# 100000 + id, entry 1).
- The five projects: the tech's icon (the picture before its animation),
  its name and a bar with the percent done. Bars move once a research period.
- Each field's share of the research points (spinners 0 to 100, shown with
  "%"), the period's research points and the shares given out.
- The research advisor's portrait and skill.

The field, the list kind and the sort column are remembered for the next
visit (player +0x538, +0x534, +0x53C). There is no auto research toggle:
the original sets it on and nothing turns it off.

## What it does

- RESEARCH, or a double click on a row, makes the picked tech the current
  field's project, if it can be researched and is no field's project yet
  . The old project stops with its progress kept, so picking it
  again later resumes it. Nothing is asked and nothing is paid.
- A spinner let go sets that field's share: the game holds it
  to what the other four leave of 100, so the total never passes 100. Each
  spinner may then grow only by what is left. Quirk kept: right after the
  window opens a spinner may go past that, and shows the number although
  the game kept the lower one; the next visit shows the real shares. Also
  kept: the "Allocated" line catches up only at the next research period.
- A click on a project shows its tech: its field's tab, its row picked,
  and the project framed. From DEVELOPED it switches back to THEORETICAL
  without refilling the list (a quirk kept).

All three orders are the research command (type 1, run):
research a tech, stop (the progress is kept by
tech), and set a share.

## Tech stats (class 18002)

The shown tech's figures, a row every 14 px from 10 px down: the caption
(STR# 18002) on the left, the value right aligned. Numbers have commas
every three digits; "+N%" adds a plus and a percent.

| Class | Rows |
|---|---|
| star drive | Range; Max Hull: Transport for class 2, Cruiser for 3, else Carrier (a quirk: the data holds only 2, 3 and 5) |
| tactical drive | Speed; Defense "+N%" |
| targeting sensor | Range: Short up to 500, Medium to 750, else Long (never Extended); Bonus "+N%"; Max Targets |
| ECM | Strength "-N%" |
| ship weapon | Type: Ship, Planet or Ship/Planet; Range: the furthest band with damage (Short, Medium, Long, Extended); Damage (the first band); Accuracy "+N%"; Reload: V. Slow from 85, Slow 65, Normal 25, Fast 5, else V. Fast; Tracking: No for 255, else Yes |
| shield, screen, field | Absorption "N%"; Max Damage; Repair/Round; Implode Yes or No |
| fighter | Speed; Defense; Hit Points; Weapons, with the two weapon names below |
| armor | Max Hull (a number); HP Multiplier with one decimal |
| installation, special | its description, centered and wrapped |
| improvement | nothing |

## The governor picker (popup 0x1B, VIEW 39000)

REPLACE opens it for the research advisor; the Foreign, Domestic and
Fleet Control windows open it for theirs. It shows the advisor and its
skill ("Research Advisor 8% "), and every governor: the system it governs
and its domestic, research, espionage and fleet skills, oldest governor
first. PROMOTE, or a double click, makes the picked governor the advisor,
in place of the one there, with no comparison and no cost; its system
gets a new governor and the list is made again. The popup stays open
until DONE. Percents round down; a turned governor's negative value goes
one lower (a quirk kept). It has no keys of its own.

## Not ported yet

When a tech is learned the port fills the list again; the
original adds and removes rows instead.
