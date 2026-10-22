# Upscaled art and text (port only)

Decided in decisions.md, "Upscaled art and a modern font".

## 2x pictures

Made at import (`UpscaleImporter`; alone with `openpax --upscale
<data folder>`): every converted picture art/set/id.png gets
art2x/set/id.png at twice the size, about a minute for all 39,176 on 4
threads, 321 MB.

The scaler (`Xbrz2x`) is our own code after the xBRZ method: for each
2x2 meeting of pixels it weighs the color differences along both
diagonals over the 4x4 pixels around it (the middle pair counting four
times) and marks a corner for blending when one diagonal is clearly the
edge (dominant when 3.6 times clearer). Each pixel then becomes four,
and per corner the pair of output pixels there is blended towards the
nearest-colored neighbor: along a shallow or steep line (2.2 times
one way than the other), along the diagonal, or as a rounded corner.
Colors are compared in YCbCr; clear and solid count as far apart.

The game draws pictures through the palette of the window showing them,
not the palette stored in their PNG, so the 2x pictures are palette
indices too. Each output pixel takes, of the indices the source pixel and
its eight neighbors use, the one nearest in color to the scaler's
result (under half cover becomes the never-drawn index 1). Taking only
indices already there keeps a picture right under any palette; the price
is fewer in-between shades than the scaler made.

## Drawing at 2x

With the UpscaledArt setting on (the default) the original screens draw
into a framebuffer of 1280x960 pixels while their code keeps the
original's coordinates (`Framebuffer.Scale`). A sprite draws its 2x
picture where it has one, else each of its pixels as a 2x2 square; fills,
lines and tints are scaled the same way. The picture is fitted to the
window keeping 4:3, sharp when it is a whole multiple of its size and
smoothed otherwise.

The game starts fullscreen at the desktop's own resolution (no display
mode change; the Fullscreen setting, Alt+Enter switches and it is kept).
A window opens at the largest whole multiple of 640x480 that leaves a
tenth of the display free, and can be resized. Runs scripted with
`--screenshot`, `--click` or `--exit-after-ms` stay a window. On a wide
display the original screens keep their 4:3 shape with bars at the
sides; filling it needs them moved onto the forms toolkit.

## Text

The TextFont setting picks how text in the game's fonts is drawn; the
layout is always the game font's own measures:

- 0, original: the bitmap fonts, each pixel a square.
- 1, upscaled: the bitmap glyph sheets through the same 2x scaler.
- 2, modern (the default): Titillium Web (SIL Open Font License, in
  data/fonts/modern with its license), filled by our own TrueType reader
  and rasterizer (`TrueTypeFont`, `GlyphRasterizer`: signed area per
  pixel, non-zero winding). Its capitals are as tall as the game font's
  (measured from the bitmap H), placed at the same point and alignment.
  In the palette framebuffer a partly covered pixel takes the palette
  entry nearest to the ink mixed with what is under it, so text stays in
  drawing order with everything else.

The modern font is somewhat wider than the game's, so a long line can
run past the box laid out for the original.

The TextOutline setting rings text in black so it reads over any
background (Jeff's call): 0 plain, 1 the ring with the game's own text
colors (the default), 2 the ring with white text. Over a system view's
starfield (wherever a map shows one system, as the Sector Display's does) the
names are white whenever outlining is on. Dark text that keeps its color is judged by what is
under it: over gray art (button captions on the silver buttons) it gets
no ring, as it stands on light art by design; over a strongly colored
highlight (a picked row's or heading's green bar: the mean spread
between brightest and dullest channel 70 or more) it turns white with the
ring. The ring is the
glyphs grown by one of the original's pixels (two at 2x), drawn under all
of a line's glyphs before they are. White everywhere loses what the
game says with color (the picked row in yellow, disabled gray, and the
green, yellow and red of how good a world is); mode 1 keeps it. Forms
text is outlined the same way.

The console switches all three while running: `art hd`, `art classic`,
`font original`, `font upscaled`, `font modern`, `outline off`,
`outline colors`, `outline white`.
