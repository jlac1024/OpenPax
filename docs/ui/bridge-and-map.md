# Bridge, game clock and galaxy map

Code: `src/OpenPax.Ui/Screens/MainControlScreen.cs`,
`SectorDisplayScreen.cs`, `Widgets/MapPane.cs`, `GameSession.cs`.

## The game clock in the app

The clock (`sim/time.md`) is one of the app's idlers, so it ticks under
every game screen while a game exists, combat included. Everything stops,
the clock too, while the app's window is not the active one.
The game's results reach the screens directly, not as a queue: notices go
to the message log, and the end of the game or of the local player opens
the end window (`victory.md`) and plays PlayerVictory or DefeatMovie over
it; another player's elimination plays OtherPlayerElim.

## Keys (the "PaxIIAccelerators" table, handler)

| Key | Does |
|---|---|
| F1 | bridge |
| F2 | Sector Display |
| F3 to F8 | Research, Fleet Command, Domestic Policy, Foreign Affairs, Planetary Surface, Ship Design |
| F9 | mode 20 |
| Esc | Game Options |
| F11 or = | game speed up, F10 or - down, within 0 to 6, with the notice "Speed set to" and the number |
| F12 or \ | pause or resume, with "Game Paused" or "Game Resumed" |

Screen keys work only in the game's screens (modes 11 to 21) and with no
popup open; speed and pause only in single player and not in Game Options,
which pauses the game itself while open. Ctrl F1 saves as "QuickSave",
Ctrl F2 loads it, Ctrl F3 loads the autosave, Alt X quits and Alt N goes
back to Game Start. The speed is kept in the GameSpeed preference and
written to the config file. Notices begin with the date and ": ".

## Bridge (mode 11, VIEW 22000, palette 0)

Seven stations are hot spots: 22001 Fleet Command, 22002 Research, 22003
Foreign Affairs, 22004 Domestic Policy, 22005 Ship Design, 22006
Planetary Surface, 22007 Game Options. Each has an animation (id + 200)
that loops from the start; while the pointer is over a station its
animation plays its highlight frames (pictures + 50), and its name (label
id + 100) shows after 100 ms. The date (22109) reads the year, a dot and
the calendar step in tenths, "2100.0" to "2100.9". The message line
(22009) shows the current notice, colored by urgency (0x7E, 0xB2, 0xE0).
The small map (22008) is the galaxy in the bridge's colors; a double
click on a star opens the Sector Display.

Two tiny hot spots beside the map (22500, 22501) count clicks; twelve in a
row show hidden pictures (Empire.img 10000 and 11000 on). Not ported.

## Sector Display (mode 12, VIEW 13000, palette 5)

The current system large, the galaxy small, and panels for the
selection: see `sector-display.md`.

## The galaxy map (class 9000, draw)

Galaxy coordinates (stars lie in 20 to 430) map to the pane as
left + width * x / 450 and top + height * y / 450, so x and y scale
separately. There is no zoom or scroll. In order:

1. The big pane's backdrop: Universe.img (star index mod 7) + 1. The small
   panes let the window's own art show.
2. The current star's marker, Infra.img 1050 + frame (1000 + frame large,
   + 50 in the bridge's colors), stepping a frame each 40 ms redraw.
3. Wormhole lines from every star the player has seen to the far ends,
   color 0x17 (0x1D), then the current star's in 0x16 (0x52). Lines run to
   unseen stars too.
4. Every star, seen or not, centered, from Universe.img (+ 100 in the
   bridge's colors): a battle there shows 20001 to 20005 cycling every
   100 ms. Otherwise unseen 20000, remembered 20009, in view 20010 + u, or
   20020 + u when the player owns a planet there, where u is the worst
   relation with the other owners there: 1 for treaty levels 4 to 6, 2 for
   1 to 3 or never met, 3 at war. Over a star in view, a mark for each kind
   of fleet present: 20030 own, 20031 friendly, 20032 neutral, 20033 hostile.
   Fleets have no sprites of their own on this map.
5. The hovered star's name after 400 ms on it, unseen stars included,
   color 0x10 when never seen, else 0x16.

A click on a star makes it current (and selects it unless a fleet is
selected). A double click acts at release: it opens the Sector Display, or
recenters there. Quirk kept: the double click action is never cleared, so
later releases on that map repeat it.

While a fleet of the player is selected and a star is hovered, its
route there is drawn hop by hop, color 0x10 where its fuel holds and 0x12
where it does not (0xB2 and 0xDF in the bridge's colors), stopping at the
first hop from a star the owner never saw. Picking and fleet
orders on the map are in `sector-display.md`.
