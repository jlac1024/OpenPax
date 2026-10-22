# Fleet Control window

Mode 17, VIEW 20000, palette 5 (ctor, handler),
reached by F4, the bridge's Fleet Command station, EXAMINE FLEET on the
Sector Display and a double click on a fleet there. Code:
`src/OpenPax.Ui/Screens/FleetCommandScreen.cs`; the game side in
`src/OpenPax.Sim/Game.ShipOrders.cs`.

## What it shows

Two pages, switched by FLEET LIST and SHIPYARD LIST:

- **Fleets**: the player's own fleets only, newest first, then sorted by
  the chosen heading (name; condition, speed, ships and strength largest
  first). Each row has an icon box with the fleet's state turning in it:
  in battle, intercepting or under orders (a 66 ms step, one counter for
  all three). Condition is the ships' average, rounded down. The right
  list holds the picked fleet's ships (condition, class and name), sorted
  by condition or name.
- **Shipyards**: the player's colonies with a shipyard: the yard's
  condition, its build points, what it builds ("IDLE", or the queue length
  and the head ship) and how far. The right list holds every design.

The preview shows the picked ship (hit points, crew experience, colonizer,
speed, strength) or design (hit points, colonizer, jump range, speed,
strength, cost and upkeep), turning in the owner's colors. Below are a
small galaxy map and the fleet advisor.

Picking a fleet makes it the selection and its star current; picking a
shipyard does the same with its planet. On opening, the selected fleet's
row is picked if it is the player's, else the first row, which is then
treated as clicked. Quirk kept: when the selection was found, the title
stays empty until a row is clicked. A double click on a fleet or shipyard
opens the Sector Display on it; a fleet in battle opens the battle window
instead, on the newest battle. A double click on a star in the map opens the
Sector Display.

The page and the headings are remembered for the next visit (player +0x518
to +0x528). Bug kept: any heading change stores the shipyard heading's
choice as the fleet column, the shipyard choice itself is never stored,
and clicks on the shipyard headings are not heard.

## What it does

- **SCRAP** asks first ("scrap the ship ..."), then the ship leaves its
  fleet with its upkeep. Nothing is refunded. Only for a fleet of the
  player out of battle; not offered when the list holds one fleet with one
  ship. An emptied fleet goes at the next star step.
- **UPGRADE**, for a fleet circling a planet whose colony has a shipyard
  (any owner's), opens the upgrade popup (0x19, VIEW 20700): the fleet's
  ships not yet marked, oldest first, and for the picked ship the player's
  newer designs of the same hull size. UPGRADE there marks the ship and it
  leaves the list; DONE closes. The fleet must have no orders. A marked
  ship is rebuilt when the yard has nothing else to do and the fleet sits
  idle there (`StartRefit`); a mark at a foreign yard never starts.
- **BUILD SHIP** with a count of 1 to 10 queues the picked design at an
  idle, whole shipyard; a double click on a design does the same.
- **STOP BUILD** removes the yard's oldest order. Quirk kept: it is
  offered for a yard that is only damaged, and there it scraps a unit of
  the colony's first damaged structure.
- **REPLACE** opens the governor picker for the fleet advisor, whose skill
  speeds every shipyard.

Scrap and refit are the ship command (type 13, run); build
and stop are the colony command. There is no rename, split, merge or
fleet mode control in this window.

## How it follows the game

The original listens to the player's and the game's broadcasts (fleets
added, removed or arriving; ships added or removed; battles; work done).
The port compares the fleets, their ships and the shipyards each frame and
fills the lists again when they change, which has the same effect: as in
the original, the ship pick goes back to the first row each time.

## Not ported yet

The upgrade popup does not
refill while open when fleets change, and is centered rather than placed
as the original places it.
