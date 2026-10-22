# Game Options window

Mode 19, VIEW 32000, palette 12 (ctor, handler),
reached by Escape and the bridge's Game Options station (22007). Code:
`src/OpenPax.Ui/Screens/GameOptionsScreen.cs`. While it is open the
game clock waits (single player); it goes on when the window is left
through the top bar or a screen key. There is no close button.

Six icons open its panels:

- **Load Game**: the saved games, LOAD (or a double click) and DELETE
  (asks first). Loading drops the game being played with no question,
  opens the bridge and says "Loaded game NAME successfully.".
- **Save Game**: the saves and a name of up to 26 characters; picking a
  save puts its name in. SAVE replaces a save of that name with no
  question, and is refused while a battle is being fought.
- **Game Options**: Play Surface Animation, Play Movies, Auto Save Game,
  Game Speed (0 to 6), and the two combat scroll speeds (5 to 25).
- **Volumes**: sound effects, CD music and voice, 0 to 10, kept as 12 per
  step.
- **Edit Planetary Profiles**: the plan library. Slots 0 to 5 hold the
  stock plans; CREATE copies the plan shown into the first empty slot as
  "Unnamed", its name can be typed, and DELETE (asks first) removes it,
  putting colonies that followed it on plan 0. Reordering by dragging is
  not ported yet.
- **New Game** goes back to the start screen and **Quit Game** plays the
  credits, autosaves if Auto Save Game is on, and closes, both with no
  question.

The Game Start screen's LOAD GAME lists the same saves.

## Keys

Ctrl F1 saves as "QuickSave", Ctrl F2 loads it, Ctrl F3 loads
"PaxAutoSaveGame". Alt X quits as Quit Game does, Alt N goes back to the
start screen. The game autosaves as "PaxAutoSaveGame" every 4000 ticks
when Auto Save Game is on.

## Settings

The settings are kept in `openpax.toml` in the user folder
(`build/dev` for dev runs), under `[preferences]`, and written when the
game closes. On the first run they start from the game's own
`Data\PrefData`. Game Speed reaches the clock at once and starts every new
or loaded game; F10 and F11 change it too. Play Movies stops every movie.

## Saves

The port's own format (`formats/port-saves.md`). Saves of the original
game copied into the same folder are listed and loaded too
(`formats/saves.md`); the port cannot write them.
