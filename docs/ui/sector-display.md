# Sector Display

The Universe window (mode 12, VIEW 13000, palette 5, handler).
Code: `src/OpenPax.Ui/Screens/SectorDisplayScreen.cs`,
`Widgets/MapPane.cs`, `Widgets/SystemView.cs`, `Widgets/InfoPanelPane.cs`,
`Widgets/FleetPanelPane.cs`; the game side in `Game.Commands.cs` and
`Game.Queries.cs`.

## Layout

Two maps: the big pane (13100, 441x261) and the small one (13101,
266x134). A swap state kept on the player (+0x52C) picks which shows the
system and which the galaxy. The VIEW data stores the big pane as the
system and the small one as the galaxy (state 1), and only that layout has
all its pictures in the game's data: the other one needs Universe.img 6000
on (stars) and 20054 to 20063 (small system icons), which do not exist,
and the original draws them without a check. So the port shows state 1 and
does not offer the swap (13102 is hidden and disabled in the data anyway).

Left: the info panel (13104) and the fleet panel (13901). Below right: a
group of buttons for the selection, and the help line (13105), which shows
STR# 13100 text for the button under the pointer.

## System view

System coordinates run 0 to 441 with the star at 220, 220. They map to the
pane as left + round(width * x / 441) and top + round(height * y / 441),
both axes by 441 although the pane is not square. In order:

1. The nebula backdrop (Universe.img (star index mod 7) + 1).
2. "Unexplored" (a literal in the executable) when the star was never
   seen, pft 12, color 0x19, right aligned at the top right, one line
   below the star's name.
3. The star, Universe.img 5000 + 50 * type + frame, 15 frames at 100 ms.
4. If the star was ever seen: its wormholes (Universe.img 8000 + frame at
   66 ms, idling through frames 0 to 7 and 37 to 44, the far star's name
   below, color 0x19 if never seen, else 0x16); its planets; and, while in
   view, its fleets not in battle.
5. Remembered but not in view: a hatch over the whole pane, every second
   row in color 0xB5.
6. The star's name, pft 12, right aligned at the top right.
7. The selection marker, Infra.img 1000 + frame (1050 + frame small).
8. After an order, a flash where it leads: Universe.img 100 to 114, one
   frame per 40 ms redraw.

Planets: the picture is the planet's base + 20 * view + spin, where the
36 views follow the orbit angle ((angle + 5) / 10, the lit side toward the
star) and the 20 spin frames step every 125 ms. The base is 300001 + 10000
* class for rocky classes 0 to 4; for gas giants 1 + 100000 * type, plus
1000 below -25 degrees (Planets.img, 720 pictures each). Over a colony in
view: the owner's flag (Empire.img 6000 + flag) at the top left and, with a
shipyard, Main.img 1100 at the bottom left. Moons circle on their own
radius and speed (Universe.img 28), then one dot per orbital unit (29) and
per mine (30); their angles move every redraw, so in the port the screen
keeps them, not the game. The orbit number (STR# 111, "I" to "VI") is under
each planet, colored by the owner's relation when seen.

Fleets: Infra.img (fleet color + 1) * 50 plus one of 32 headings toward
the first order, then the owner's flag; Main.img 1101 marks one set to
intercept. The ships' designs do not change the picture.

The relation color is 0x16 for one's own and for treaty levels 4 to 6,
0x10 for 1 to 3, 0x12 at war and 0xFF for players never met.

## Picking

The galaxy picks the nearest star within 40 galaxy units. The system picks
within 20 system units on both axes: fleets not in battle, then planets,
then wormholes, then the star; the nearest by whole distance wins, an
earlier kind on ties. A single click selects; wormholes, fleets out of
view and gas giants cannot be selected. A double click acts at release:
on a fleet, the Fleet Control window (`fleet-command.md`), as does a
fleet's Examine; on a planet, Planetary Surface; on a wormhole the far star becomes current; on a star it recenters, or opens the battle window when a battle is under way there.

Making a star current replaces the selection with the star, unless a
fleet is selected: a fleet stays selected.

## Panels

- A star (or nothing): the empty group 13600. The info panel lists its
  planets: picture, flag, name, population (own colonies) or the
  population the player could have there, habitability, and wealth
  (STR# 14001).
- A planet: group 13200 with Examine; Colonize when it has no colony and a
  fleet of the player there carries a colony ship. The info panel shows
  the planet's picture with city lights and defenses and their health,
  and its wealth, population (with a bar), income, espionage, research,
  construction and strength for own colonies.
- A fleet: the fleet panel (a cell per ship, framed by condition, with
  experience) and group 13220: for one's own, Examine, Move, Attack,
  Interdict, and Colonize when it carries a colony ship. The info panel
  shows speed, condition, strength, jump range and ships.

## Orders

With a fleet of the player selected, the right button (a left press
flagged as right) or an order mode set by Move (1), Attack (2)
or Colonize (3) makes the next click an order instead of a pick. After it
both maps go back to no mode.

- On the galaxy: a route to the star, a fleet order per
  wormhole on the way, replacing the old orders; refused when out of
  range, and in the attack and colonize modes.
- In the system, the fleet there: by what is under the point, replacing
  the old orders: another fleet (attack if foreign in mode 2; else move to
  it), a planet (colonize if free and not a gas giant in mode 3; attack if
  foreign-owned in mode 2; else move), a wormhole (if the far star is in
  range), the star (orbit it), or empty space (move to the point).
- In the system, the fleet elsewhere: first the route there, then that
  order appended.
- Interdict toggles the fleet's intercept flag at once. The planet's
  Colonize sends the first fleet there with a colony ship to it, set to
  colonize. The "c" key colonizes at once from a fleet orbiting a free
  planet (not wired to a key yet).

In single player the original makes each order a command, queues it and
runs the queue at once, inside the click; the port calls the game directly. A
voice answers each order (`sound.md`).

Clicking a ship in the fleet panel of one's own fleet unselects it (drawn
dim, with the other rank pictures) or selects it again; the last selected
ship cannot be unselected, and showing a fleet selects all its ships
again. Before any accepted order to that fleet,
from the maps or the planet's Colonize, the unselected ships are split off
: they leave for a new fleet in the same place,
named as a new fleet of that system, with the fleet's mode and intercept
setting, and stay behind while the rest carry out the order. A route to
another system splits only once its range check passes.

## Colony actions

For the player's own colony, not in battle, the planet's buttons are
: Abandon always; Build a Ship with a shipyard, nothing in its
queue and no structure below full health; Build/Upgrade Orbital with
nothing in the queue and some structure allowed; Stop with a ship queued
or a structure below full health; Scrap with any structure. All of them
are one colony command (type 0, run), which checks again
that the planet has a colony and is not in battle.

- **Build a Ship** opens the build panel (group 13920) in place of the
  details: the planet's name, the player's designs in a list sorted by
  class or by name (the headings TYPE and NAME; the column is remembered),
  the picked design turning in the player's colors with its figures, and
  "Build how many?" (1 to 10). BUILD SHIP, or a double click on a row,
  puts that many of the design at the front of the first shipyard's queue,
  whatever the yard's health. The design last picked is picked again next
  time.
- **Build/Upgrade Orbital** (group 13230): fighter base, missile base,
  battle station, mine field, academy, shipyard, each shown when allowed
  (`sim/economy.md`); a click orders the best known tech of the kind, a
  new stack or one more unit.
- **Scrap** (group 13240): a button for each kind present; after "Are you
  sure you want to scrap/downgrade the ...?" one unit of the first stack of
  that kind goes, with its upkeep. Nothing is refunded.
- **Stop**: the oldest ship order leaves the queue (a refit's old ship
  goes back to a fleet at the planet); with none queued, one unit of the
  first structure below full health goes. Quirk kept: that also scraps a
  unit that is only damaged in battle. Work spent is lost.
- **Abandon**: after "Are you sure you want to abandon ...?" the colony is
  destroyed, which can eliminate the player. A nomad species first leaves
  in one colony ship per ten population, plus one, in an "Abandon World
  Fleet" (a "Transport" design is made if it has no colony ship design).

The yes or no question is the popup of kind 0x1A (VIEW 38500): it takes
the mouse and keys while open (Return and Tab answer yes), refuses the
screen keys, and leaves the game clock running. The port centers it on
screen; where the original puts it is open.

## The governor panel

GOVERNOR (13103) shows the panel 13800 in place of the details on the
left (the help line stays), unless the player has been eliminated; pressed
again, or when the selection changes, the selection's panels come back.
It shows the player's governor of the current system: "Governor of" the
system's name "sector", the portrait, and its skill in each field
(Domestic, Research, Espionage, Fleet) as a truncated percentage,
negative for a governor turned by an enemy. FIRE replaces it with a new
governor at once, at no cost (command).

## Work line and battle markers

The planet panel's last line says what one's own colony is
working on: the ships queued at its shipyard with the first one's class,
name and progress ("2 / SC0: Scout 40% "); else the first structure being
built or repaired ("Level 2 Missile Base 60% "); else the next
improvement and its progress; else nothing.

A battle shows its marker in the system view (Universe 1000 on, 15
frames at 100 ms) where the first fleet stood as it joined;
strategic time stands still during a battle, so it never moves. A double
click on it opens the battle window (it is looked for before anything
else); a single click on it selects nothing (the original selects the
marker, which changes no panel).

The small system look's hover names follow, but that look is
never shown: the port does not offer the swap.

Own colonies also show the last period's population growth, ten times
over to one decimal, after a "+" (even for a loss), above the population
figures. Fleet speed shows to one decimal too.

A nomad species abandoning a colony with more than 210 people gets one
colony ship per ten people plus one, but its fleet takes 21 at most: the
rest are refused, as in the original, though each still uses a ship id
and still announces itself as a ship finished.
