# End window

Mode 0, VIEW 16000 ("victory window", handler).
It opens when the game ends or the local player is eliminated, and the
victory or defeat movie plays over it. Code:
`src/OpenPax.Ui/Screens/VictoryScreen.cs`.

Two looks by whether the local player is still in: a winner gets palette
13 and background 16000, a loser palette 14 and background 16001; the
three buttons take the pictures 16005 on or 16002 on to match their
background (in the defeat art the panel is a dimmed crop of the bright
explosion, so it shows pale). The line at the bottom is STR# 110: VICTORY
or DEFEAT. "Simulation complete" plays as it opens; the music is the
game's.

- Quit Pax: the app's quit command (the credits, an autosave when the
  preference is on, then the game closes).
- New Game: the game is dropped and the start screen opens.
- Observe Game: every system is shown to the player (the game command
  of kind 7) and the bridge opens, so a defeated player can watch the
  others play on. After a victory the game has ended and its clock no
  longer runs.
