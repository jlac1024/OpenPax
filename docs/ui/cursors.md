# Mouse pointers

The original hides the system pointer and draws its own (
picked, drawn). A pointer set is 20 Main.img
pictures from the set's number on, stepped every 50 ms and looping. Only
one set shows at a time; whatever picked it last wins, and it stays until
something picks another. Quirk kept: the picture count is not reset when
the set changes, so a new set starts at the picture the last one reached.
Code: `src/OpenPax.Ui/MouseCursor.cs`.

## Sets

| Set | Use | Point on the mouse |
|---|---|---|
| 50 | arrow | 7, 2 |
| 90 | over a spinner's value box (drag to change) | 14, 14 |
| 110 | a move or battle command waiting for a place | 14, 14 |
| 130 | attack order over an enemy | 15, 15 |
| 150 | dragging a list row with the right button | 14, 14 |
| 170 | Game Start | 7, 2 |
| 190 | the bridge | 7, 2 |
| 210 | Species Selection | 7, 2 |
| 230 | colonize order over a free planet | 15, 15 |
| 250 | over a Species Selection portrait (its hot spots' own pointer) | 15, 15 |
| 270 | Game Start's second variant (VIEW 31001) | 7, 2 |
| 290 | colonize order elsewhere | 15, 15 |
| 310 | attack order elsewhere | 15, 15 |
| 330 | Player Design | 7, 2 |
| 370 | the end window, won | 7, 2 |
| 390 | the end window, lost | 7, 2 |

Sets 70 and 350 get a point (14, 14) but nothing picks them.

## Who picks

- Windows as they open: the ones in the table above, and the arrow for
  Game Setup, Research, Domestic Policy, Fleet Command, Foreign Affairs,
  Ship Design, Game Options, the battle window, the developer window and
  the governor, mission, compare and yes/no popups. The Sector Display,
  the Planetary Surface and the trade, offer and upgrade popups leave the
  pointer as it was.
- A hot spot whose layout names a pointer shows it while the mouse is over
  it, keeping the one it replaced and giving it back when the mouse leaves.
- A spinner shows 90 over its value box and the arrow elsewhere and when
  the mouse leaves.
- The galaxy and system maps follow the order waiting, on
  every frame the mouse is over them and when the order changes: the arrow
  with none (unless the spinner's 90 is showing), 110 to move, 130 or 310
  to attack (another's fleet, or a planet with another's colony, under the
  mouse in the system view), 230 or 290 to colonize (a free planet that is
  not Class 5).
- The battle field: a command that waits for a place shows
  110, AUTO shows the arrow, and the others leave it; giving the waiting
  command brings back the arrow.

## In the port

The host hides the system pointer and the app draws the set over
everything at the mouse, minus the set's point. Nothing is drawn until the
mouse has been seen over the window, or after it leaves.

Not ported: dragging list rows with the right button, and with it set 150.
