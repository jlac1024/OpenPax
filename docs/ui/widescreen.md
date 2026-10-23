# Widescreen and window size

The original screens are 640x480. They are drawn at a whole-number scale
of the window, so every pixel of art and text lands on one of the
window's own pixels: sharp, never smoothed (Jeff, 2026-10-21).

## Scale and frame size

The scale (`UiApp.FrameScale`) is the largest whole number at which
640x480 fits the window, kept even while the upscaled art is on (it is
drawn at 2x and its multiples): 2 at 1920x1080 and at 2560x1440, 4 at
3840x2160, 1 below 1280x960.

Below 2x (a window under 1280x960, such as a laptop's 1366x768) no screen
grows: wider would only make it wide and small. Every screen stays
640x480 and is scaled to fill the window as far as it goes with its shape
kept, smoothed as it is not a whole multiple: 1024x768 at 1366x768, with
starfield beside it (Jeff, 2026-10-23). A window smaller than 640x480 gets
the picture shrunk the same way.

From 2x up, a screen that grows (`Screen.WideAt`) is drawn as large as the
window at that scale: as wide as the window (up to 21:9), and as tall when
it has row spans; 960x540 at 1920x1080. Any other screen stays 640x480,
and its picture sits centered at the whole-number scale with the rest of
the window as bars showing the starfield (moving beside the menus, still
beside the game, the MovingStars setting). A screen with column spans and
no row spans is wide but 480 tall, between bands of starfield.

## How a screen grows

A screen that grows names its spans (`Screen.WideAt`, a `WideLayout`):
columns and rows of its 640x480 layout and art where the extra width and
height go, each set in equal shares. Laid out larger by some amount:

- a pane moves by the shares of the spans before it (left of it, above it);
- a pane grows by the shares of the spans it crosses, except that a
  button, and anything 24 rows tall or less (a spinner, a label), keeps its
  height and moves down by a row span's share when its middle lies past
  the span's middle;
- the background picture is cut by the spans across and down; each piece
  between spans is drawn as it is, moved by the shares before it, and each
  piece of a span is stretched by its share.
  Rows in a tiled band (a ruler) repeat at the band's period instead, so
  the ticks keep their spacing. A layout with `RepeatRows` repeats its row
  spans' rows down instead of stretching them: a span exactly one period
  of a grid keeps the grid's lines evenly spaced.

Every pane is placed from where the layout first put it
(`Pane.Original`), so widening again, or back to 640 when the window
changes shape, is exact. A screen moves what the plain rule does not suit
in `Widened`.

Spans are chosen where every row of the art is plain: metal, an empty view,
a ruler (tiled). Stretching keeps the metal's look and has no joints.
Repeating a strip of it showed a seam at every joint, and mirroring every
other copy showed a pattern of crosses; both were dropped.

A scroll bar that grows stretches its track to its length.

A picture that is a sign rather than panel art keeps its shape in a grown
pane, centered (`PicturePane.KeepShape`): Foreign Affairs' cross over an
eliminated empire, which stretched into a blob.

Popups are centered on the wider screen. The game screens' top bar widens
in its message line (columns 100 to 400): the lamp, the date and Back move
right with the width, and the message line grows.

## Screens

| Screen | Spans | Notes |
|---|---|---|
| Sector Display (mode 12) | columns 268 to 288 and 333 to 355; rows 245 to 259 (in the system view) and 426 to 440 (in the galaxy map), each one 15 row period of the planet list's grid and repeated, not stretched, so the grid stays square (stretched, it warped and streaked the planet panel); the second starts below the galaxy map's and the lower right panel's line at 424 and 425, which would otherwise repeat; rows 296 to 307 and 442 to 452 tiled every 17 | either side of the bottom bar's ornament, which stays centered, between the galaxy map's notches and clear of the knobs. The rulers tile at their ticks' spacing. Both maps' panes take the whole room (background and clicks), and their content keeps its original shape, as large as fits, centered (`MapPane.KeepShape`): the system with its star in the middle of the view, the galaxy in its frame, as positions would otherwise stretch. The system view's nebula fills the grown pane, every other copy across mirrored. |

| Research (mode 13) | 172 to 175 | the gap between the second and third tabs, left of the grid lines at 175 (tabs) and 176 (detail panel). The tech list gives the room to its names: its type and cost columns keep their place from the right (`TechRow`), and the headings follow the layout (`HeaderBarPane` maps its cells). The detail panel grows; its figures (18071) keep their size at its right, and the tech's picture (18200) sits under the title in the middle of the panel, or as near as it can without running into the figures. |

| Fleet Command (mode 17) | 70 to 95; rows 299 to 310 and 445 to 452 tiled every 17 | inside the lists' name column, clear of the left oval and the bottom bar's grille. The fleet and shipyard lists give the room to their names (rows move their other columns by `ListRow.Grown`), FLEET LIST stretches with its slot, the galaxy keeps its shape in its widened frame, the advisor and the fleet's panel move over. |

| Domestic Policy (mode 16) | 326 to 336, the planet table's column line, between two filter boxes; rows 205 to 460 tiled every 88, rows 28 to 35 and 172 to 179 every 17; width in steps of 88 | the table's rows repeat a whole planet column, so the list shows more planets; the width grows by whole columns (`WideLayout.WidthStep`) and the rest is bars, so the tiled columns close cleanly. The galaxy keeps its shape, the advisor and REPLACE move over. |

| Foreign Affairs (mode 18) | 339 to 347, the eighth empire slot's edge; rows 210 to 240 tiled every 37, rows 290 to 471 every 15; width in steps of 37 | in the missions list, the points box and the gap between treaty status and communications. The slot row repeats a slot, so it shows more empires; the lower grid repeats its cells. The missions list and ABORT grow; the info bar's status heading and value move over with the other figures, so the room opens after the empire's name. |

| Ship Design (mode 15) | 289 to 296, between the DESIGN slot and the grilles; rows 22 to 52 (the top bar's rail) tiled every 8 | right of the stepped hull buttons. The ship view, the overview and the design panels' lists grow; the shield diagram (26039) and the figures by the view (26035) keep their size and move over, the maintenance figure stays by its label; the button column moves over. The top bar's rail repeats there, as its columns differ and stretching them showed blocks. |

| Planetary Surface (mode 14) | 508 to 522, a line of the figures block's grid; rows 362 to 477 tiled every 15; width in steps of 15 | the figures block, its name box and grid grow (the figures, 14014, keep their size, centered in it); the orbitals and the middle block stay. The surface (14001) spans the window and keeps its landscape and buildings centered, their own size, with plain black either side behind a thin edge (`SurfacePane`), drawn in the nearest colors of the planet's palette. Reflections of the landscape were tried first (the mountains read as mirrored), then a starfield. The gaps between the blocks were tried first: stretched they streaked, repeated they ribbed. |

| Game Options (mode 19) | 440 to 454, a line of the grid left of every spinner; rows 25 to 474 tiled every 15; width in steps of 15 | the panels and the save list grow, the icons and spinners right of the line move over; labels across it keep their size, a title staying over the gap in its frame, a spinner's heading moving with it. |

| Combat (mode 20) | 191 to 192, the controls panel's first button slot, where the prompt line is plain black; rows 363 to 427 tiled every 52 (the slots), rows 456 to 475 every 15 (the bottom box); width in steps of 52 | the battlefield (a view onto its 3600 wide field) shows more of it across; the panel gains empty slots and its prompt line grows; the slot buttons and the indicators keep their size and spacing, all moved over together; AUTO moves over. A span on the panel's left edge was tried first; it smeared the edge's grille. |

A pane that grows draws its own background picture, or a picture pane its
picture when it fills the pane (a panel's art), stretched where the
window's spans cross it (`Pane.DrawFitted`); a smaller picture in a grown
pane is drawn at its own size.

A button that crosses a span draws its picture with the middle third
stretched (`ButtonPane`), so it grows with its slot in the art; its
caption keeps its alignment.

The bridge (mode 11) is a picture of a room and stays 4:3, as do the
setup screens. Research and Fleet Command grow across only for now (their lists' ruled
rows would not stretch down cleanly); other game screens are to follow
one at a time.
