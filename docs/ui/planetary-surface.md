# Planetary Surface window

Mode 14, VIEW 14000 (ctor, handler), reached by F7,
the bridge's Planetary Surface station (22006), EXAMINE PLANET or a double
click on a planet in the Sector Display, and a double click on a column of
the Domestic Policy window. Code:
`src/OpenPax.Ui/Screens/PlanetarySurfaceScreen.cs` with `SurfacePane`
(class 14001), `ColonyStatsPane` (14900) and `WorkRow`; the game side in
`src/OpenPax.Sim/Game.SurfaceQueries.cs`.

## Which planet

The selected planet; else, if the current star has been seen, its first
planet that is not a gas giant; else the newest colony; else the first
fleet's star. The original crashes when that star has only gas giants; the
port then takes the newest colony. The colony arrows walk the player's
colonies, newest first, wrapping. The planet shown becomes the selection,
but the current star is not changed (a quirk kept). The palette follows the
planet's class (palette 6 plus the class).

## What it shows

- **The surface**: the landscape of the planet's class, replaced as
  population improvements are finished; for each of credits, research,
  espionage and construction a building stack that grows with the finished
  improvements of that resource (ten at most); the improvement being built
  on its stack with a crane, and a short "work starts" animation when it
  changes; crowds by population (five tiers) and eight ambient loops that
  need more people. A planet with no colony in view shows bare. Foreign
  colonies in view show in full (a quirk kept). Animations step every 66 ms
  of screen time; the PlaySurfaceAnim preference turns them off (always on
  until the config file exists).
- **The work queue**, the player's own colonies only: the ship the yard is
  building or a structure being repaired, then the improvements not yet
  finished in plan order. Collapsed, only the top line (the current work)
  shows; EXTEND LIST grows it upward over the surface.
- **The plan** (the colony's governor), stepped with the arrows through
  the six stock plans; a planet in battle keeps its plan.
- **CP Subsidy**: construction points paid for this colony, 0 to 2000. The
  order (command 10, action 1050) changes the colony's subsidy and the
  player's total by the same amount, neither below 0. Each unit costs 2
  credits a period and adds 0.1 construction.
- **Annual Income**: the empire's net income (the Domestic window's Net
  Income, though the caption says Annual Income).
- **Figures**: habitability, wealth, and for an own colony its growth
  (quirk kept: "+-0.3" when shrinking), population with a bar, income less
  upkeep, espionage, research and construction; "--" otherwise.
- **Orbitals**: an icon for each kind of structure the colony has, when
  its star is in view; refreshed only when the planet changes (a quirk
  kept).

Nothing is built, scrapped or sold here; that is the Sector Display's
planet panel.

## How it follows the game

The original listens to the player's broadcasts (improvements and
structures changed, ships finished, techs learned, the economy pass). The
port compares the colony's work and plan each frame and the income and
subsidy once a period.

## Tooltips (the port's own)

The pointer resting 0.6 seconds on a figure, CP Subsidy, Annual Income, the plan, the orbitals, the queue or the colony arrows shows what it is for, in a
true-color box by the pointer (`Screen.TooltipAt`, the texts in `UiText`).
The original has labels only.
