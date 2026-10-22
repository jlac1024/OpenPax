# Notices, the message log and the top bar

Code: `src/OpenPax.Sim/Game.Notices.cs` (the game side),
`src/OpenPax.Ui/MessageLog.cs`, `NoticeText.cs`, `UiNotice.cs`,
`Widgets/TopBarPane.cs`, and the back stack in `UiApp.cs`.

## Where notices come from

The original calls a player virtual during the tick and the player posts a
notice only when it is the local one. The port's game keeps a list of
notices for human players (computer players get none, so headless games
keep none) and the screens take them after each frame. Each starts with
the date and ": " unless said otherwise.

| Event | Text | Urgency | Double click | Original |
|---|---|---|---|---|
| research finished | "Research completed on " + tech | 0 | Research | |
| scientist bonus, or a tech stolen while it was being researched (quirk kept) | "Breakthrough! Your scientists discover " + tech | 0 | Research | |
| ship finished (also each ship of a nomad abandon fleet) | "Finished construction of " + ship + " in " + star + " Sector" | 0 | that system, the yard selected | |
| a colony lost (destroyed, taken, given away, abandoned) | planet + " Defeated" | 2 | that system, the planet selected | |
| the war slot changes | "War with " + player | 2 | Foreign Affairs | |
| first contact (also during setup, with a neighbor one wormhole away) | "First contact with " + player, then the First Contact movie | 1 | Foreign Affairs | |
| another player eliminated | player + " was eliminated!" (after its movie) | 1 | Foreign Affairs | |
| espionage | the mission's own words (STR# 200 + kind): stole, failed, caught, stopped, done to you | 0 to 2 | Foreign Affairs | |
| trade | "Trade package Offered by", "accepted by", "rejected by", "failed, missing items -" + player | 0 or 1 | Foreign Affairs | |
| game speed, pause | "Speed set to " + speed, "Game Paused", "Game Resumed": no date, transient | 0 | none | |

Nothing is posted for a treaty offer (only a sound), a save, a battle, a
new colony or a fleet's arrival. Making the clock posts the speed once at
the start of a game.

Open: the espionage missions' own result words (what was stolen or hit) are
not read yet; the count of things hit stands in for them.

## The log

Notices queue first in, first out, without limit. A free-running timer
makes the next one current every 3000 ms, but one shows at once when it,
or the current one, is transient. The current notice and the older lasting
ones form a history of 150, newest first; a transient notice is never
kept. A new lasting notice makes the lamp blink 9 times, 250 ms apart.
A new game clears the history (quirk kept: not the queue).

## The top bar (game windows from the Sector Display on)

Main.img 1000 across the top, added last to each window so it draws on top:

- the current notice at x 36, colored by urgency (0x16, 0x10, 0x12; 0xFF
  under the pointer); a double click runs its action;
- the lamp (497, 3): 1023, 1024 or 1025 by urgency, flashing while it
  blinks; a click opens or closes the history, a drop-down (Main.img 1001)
  of the 9 newest notices, where a double click runs a notice's action;
- the date, right aligned at 588;
- the back button (601, 0; 1011, 1010 pressed): back to the screen before,
  or with the right button straight to the bridge.

A popup closes the history. The bridge has no top bar; its message line
(22009) shows the current notice, and a double click runs its action.

## The way back

Switching screens from the keys, a notice or the bridge remembers the
game screen left, unless it is already the newest remembered; leaving the
bridge forgets them all. Back goes to the newest remembered screen that is
not the current one (dropping repeats), else the bridge.
