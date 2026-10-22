# The system's sky (the port's own)

Behind the Sector Display's system view the original shows one of seven
nebula pictures (Universe.img 1 to 7, by star number). The port draws a
sky made for each system instead (`Widgets/SystemSky.cs`), the
MadeSkies setting in Options; with it off the original pictures show,
tiled across a widened view.

## Always the same for a system

The seed is the FNV-1a hash of the star's name, so a system shows the same
sky every visit, in every game where it has that name, on every machine.
The generator has its own xorshift random numbers and never touches the
game's; it is presentation only.

## What it draws

In order, from the seed:

1. A nebula on a little over half of systems: fractal value noise (five
   layers, each turned and moved off the others, the point warped by a
   slower noise so the lattice never shows as straight edges), cut off
   below a threshold, with dust lanes darkening it and its color turning
   between two of six space hues (violet, blue, red, teal, amber, indigo).
2. Stars: a fine dust of faint ones, fewer brighter ones, and a handful
   with a soft glow and four short spikes, tinted white, blue, yellow,
   orange or red.
Distant spiral galaxies were tried as a third layer and dropped: in the
system view they read as the game's wormholes.

## Into the palette

It is painted in true color at the frame's own pixels (the view's size
times the frame scale, so it stays sharp), then each pixel takes the
nearest entry of the screen's palette (weighted towards green, as the eye
is), after a 4x4 ordered dither so faint light does not band. The
never-drawn index is never chosen. A sky takes 50 to 200 ms at 2x and is
kept until the system, size or palette changes.
