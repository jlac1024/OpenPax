# Frame pacing (the port's own)

The main loop runs at the primary display's refresh rate (60 when SDL
cannot tell). With the VSync setting on (the default), presenting a frame
waits for the display's refresh, which sets the pace. Without it, or when
presenting returns far too soon (a minimized or hidden window),
`FramePacer` ends each frame at its own deadline on SDL's nanosecond clock
with `SDL_DelayPrecise`.

A frame that ran more than a frame late starts the next one from now,
rather than rushing several frames to catch up. With vsync working, the
pacer never sleeps after a full-length frame: sleeping again could miss
the next refresh and halve the rate.

The loop used to sleep a fixed 16 ms after each frame's work, so frames
ran below the display's rate and unevenly.

The game clock is untouched by any of this: it reads real time and steps
the game by its own speed setting (sim/time.md). The console's `fps`
command shows the last frame's rate and length.
