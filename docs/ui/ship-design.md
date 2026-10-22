# Ship Design window

Mode 15, VIEW 26000, palette 11 (ctor, handler),
reached by F8 and the bridge's Ship Design station (22005) only. Code:
`src/OpenPax.Ui/Screens/ShipDesignScreen.cs` with the panes
`WeaponArcsPane` (class 19500), `DesignOverviewPane` (26001),
`SlotPanelPane` (26002) and `ComponentRow`; the game side in
`src/OpenPax.Sim/Game.DesignOrders.cs` and `Design.Remove`,
`Design.Firepower`.

## Browsing

The player's designs are shown one at a time; the arrows step to the
previous or next filled slot, wrapping round. The window shows the class
and name, an overview (hull, strength, hit points, and the raw figures of
the star drive, tactical drive, shield, ECM and targeting, a missing
system's caption in red), the special systems fitted, the build points and
maintenance, and the firing arcs.

The firing arcs show the design from above with a wedge for each arc (bow,
port, starboard, point defense) and range band where it has firepower,
with the damage written in. A weapon's mode is its arc. Planet bombs are
left out of the sums; band 3 is never drawn.

DELETE asks, then empties the slot (ships already built keep their own
copy) and shows the next design. The original sends no refresh when the
design in slot 0 is deleted and keeps showing it; the port steps on.

## Making a design

DESIGN starts a copy of the shown design (or a blank Scout when there is
none), named "Unnamed", with the player's best armor fitted. There is no
editing in place: the copy is saved as a new design with a new class
number, and the old one stays until deleted.

The category buttons on the right pick what to fit:

- **Hull**: Scout to Carrier; those larger than the best armor allows are
  disabled. Changing the hull throws away every component and resets the
  armor class to NONE.
- **Armor**: the armor class, NONE (half the hit points) to HEAVY (double).
- **Star, Tac, ECM, Targ, Shield**: one system each. Star drives must
  match the hull's class.
- **Primary, Port, Starboard, Point**: weapons on that arc (point defense
  takes only energy and projectile weapons).
- **Special**: repair systems, colonizers, fighters, and only the best
  minesweeper.

The list holds the known techs of the category no bigger than the whole
hull, with their space and cost, sortable by name, space or cost. ADD (or
a double click) fits one; another of the same tech and arc raises the
count. REMOVE takes one away. Quirk kept: free space is not checked, so a
design can be filled past its hull. A category button is red while that
part of the design is empty.

AUTO fills a blank design of the hull as computer players' designs are
filled (the best armor at HEAVY, drives, sensor, ECM, shield, fighters,
colonizer and weapons). CLEAR starts the hull again, CANCEL drops the
design.

ACCEPT needs the design to fit its hull, a tactical drive and a name
(10 characters at most). The design is copied into the first free slot
with the next class number for its hull (for example DD3) and a new design
id, and shown. With all 100 slots taken nothing happens (a quirk kept).

The design being made is kept when the window is left and resumed on the
next visit. The category, the design shown and the two sort captions are
remembered too; nothing sets them before the first visit, so the port
starts on Overview, the first design, sorted by name.

## Not ported yet

Freeing a design in progress that could not be saved when the
window closes (it stays until CANCEL here). The exact text positions in
the single system panel. Whether the design in progress is saved with the
game.
