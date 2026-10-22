# Battle window

Mode 20, VIEW 24000 (ctor, open, handler),
palette 5. Reached by F9, a double click on a star with a battle under way in
the Sector Display, or activating a fleet in battle in Fleet Command. The game
never opens it by itself: a battle runs whether or not it is shown, and
strategic time stands still until every battle is over. Code:
`src/OpenPax.Ui/Screens/CombatScreen.cs` with `Battle/BattleFieldPane`
(class 24001), `Battle/BattleMinimapPane` (class 24002) and `Battle/BattleArt`
(which pictures each object shows). Study notes:
build/scratch-ui/combat-window.md and combat-draw.md.

## What it shows

The first battle under way, centered on the local side: its newest ship,
else orbital, else the planet's corner if the planet is the player's. The
field is 3600 square, seen from 35 degrees (depth times 0.5736, rounded),
scrolled but never zoomed. Stars are tiled behind at half the scroll speed
(quirk kept: the vertical offset adds the view's width); a planet battle
has the planet's backdrop in the far corner with its blocks, standing or
ruined. Then, in this order: explosions of ships, mines (black where the
side cannot see them), shots (rounds side by side across their line of
flight), the selection band, orbitals and fighters out, ships column by
column through the field's grid, and flashes on top.

- Ships show their owner's fleet color and hull, one of 32 facing
  pictures, and engine frames while moving. A ship chosen or marked is
  outlined in the relation color (own 25, at war 18, under a treaty 96,
  otherwise 115) with its name and a health bar.
- Orbitals and mines animate every 83 ms of real time; a marked orbital
  has its outline and health bar. Fighter craft keep fixed offsets that do
  not turn with them.
- Effects show one frame per battle step; shield flashes the original's
  art lacks are skipped.

The minimap shows ships and orbitals as dots, fighters out as pixels, the
planet, and the box the field shows. Left drag scrolls; a right click moves
the chosen ships there.

## Commands

- Left click: an own ship is chosen alone, an own orbital or an enemy is
  only marked, empty space starts a band that chooses every own ship it
  meets. Right click with ships chosen: empty space moves them as a group,
  an enemy is attacked, an own ship joins the choice.
- The buttons (CONTROLS shows the bar; it stays as left for the next
  battle): Move, Attack, Target, Formation Controls (several ships), Face,
  Escort (it does nothing, as in the original), Fighter Controls (when the
  chosen ships have fighter bays), Retreat; the fighter page: attack with
  fighters, defend with fighters, recall, auto launch; the formation page:
  four lines. A command that needs a point or a target waits for a click
  on the field, with its prompt; the rest act at once. Pointing at a
  button names it in the prompt line.
- Keys: m f t a (move, face, target, attack), q w e r (fighter commands),
  h v l u (formations), x retreat, o AUTO, Backspace drops a waiting
  command, arrows scroll (by the Combat Key Scroll setting), Space centers
  on what is marked or chosen, 0 chooses every ship, 1 to 9 a group, and
  shift with a digit (! @ # ...) gives the chosen ships that group. Groups
  are kept with the ships.
- AUTO hands the side to the computer: it thinks as a computer side does,
  but never gives up or breaks off. Pressing it again leaves every ship
  idle.
- With orbitals marked and no ships chosen, the fighter commands go to the
  marked orbital.

Every command becomes a battle order (kind, battle, target, orbital, point
and ship ids) that the game applies; the window never changes the battle
itself.

## Leaving

When its battle ends the window goes to the Sector Display; it picks up a
new battle if it was showing none.

## Not done

The edge scroll is unreachable in the original's layout and is left
out.
