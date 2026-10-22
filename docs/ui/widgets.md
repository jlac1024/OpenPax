# Widgets

How the original draws its window elements and takes input, as far as the
ported screens use them. Code: `src/OpenPax.Ui/Widgets/`.

## Windows and panes

Every element is a pane: an id, a rect in window coordinates,
a parent, and visible and enabled states. The VIEW's visible 1 means "on
unless the parent is off". A window (class 8000) has a palette
index and a background picture from Main.img, which is usually but not
always its own id (Player Design, VIEW 11300, uses 25000).

- Each pane draws clipped to its own rect, after its parent.
- The pane under the pointer is found by testing children last to first,
  so later VIEW entries are on top. It hears it is there every
  frame, not only on entry, and hears when the pointer leaves.
- A press goes to that pane, which keeps the mouse until release.
- Widgets report to the window as (message, value): usually their own id.
  Hover is 3001 and 3002, hot spot press 3007 and release 3008, text edit
  change 1106 and done 1107.

## Classes

| Class | Kind | Draws | Input |
|---|---|---|---|
| 8002 | button | picture, +100 pressed, +200 disabled (else the plain one); text with its font, style, colors | click sends its id; a latching button stays down after a click until code releases it, and ignores presses while down |
| 8009 | picture | one picture at its corner, from a chosen image file | none |
| 8016 | check box | box picture, +100 checked (a disabled box always shows unchecked); text after the box, baseline bottom - (line height - ascent + 1) | flips on release over it and sends (id, value) |
| 8017 | text edit | one line scrolled to keep the caret 1 to width - 3 px in, caret a palette 25 line blinking every 333 ms; not editable, wrapped text | first click only takes the keyboard and selects all; double click selects all; typing stops when the length equals the maximum (a selection is replaced without the check) |
| 8021 | hot spot | nothing | counts as enabled when hidden, so hidden hot spots take clicks |
| 8024 | wrapped text | words split on space and line feed (a line feed is not a break), lines filled while narrower than the rect, an over-long word broken by characters, lines placed by cell top | none |
| 8025 | label | STR# text, baseline bottom - (line height - ascent); color 10 disabled, 16 highlighted, 22 marked, else its own | hover messages |
| 8027 | spinner | decrease arrow, value box, increase arrow side by side from its left; value text centered | arrows step by one and repeat after 500 ms, then every 25 ms; dragging the box changes the value by one per pixel; the value is sent only on release |
| 8028 | text line | one line, baseline as the label | none |
| 8019 | list | rows of 14 px stacked from the top, scrolled; the build list's rows show the class name at +5 and the design name at +40, color 28 selected, 159 not | a press selects the row under the pointer and follows it while held; a double click sends the activate message at release; no keys |
| 8033 | scroll bar | arrows at each end, the track between (+50 when there is nothing to scroll), the thumb placed by the list's fraction | arrows step a row, repeating after 500 ms then every 100 ms; the track jumps the thumb to the pointer and drags it |
| 8034 | header bar | headings, the chosen one filled with its color | choosing a clickable heading sorts its list by the heading's code (the C library's sort) |
| 8038 | design preview | the design turning, Combat.img ((hull + (fleet color + 1) * 10) * 100 + frame + 1, 32 frames at 66 ms), with its figures | none |
| 8039 | animated button | its slot picture (+100 pressed, no disabled picture), then an icon from Main.img at frame 0, playing at 66 ms a frame while the pointer is over it | as a button |
| 8037 | animation | first picture + frame from its image file, never erased | plays forward or backward at 1000 / fps ms a frame; a play that does not loop sends its id at the end; never takes clicks |

Fonts: line height is the regular face's metric 0; single line text uses
the bold face's ascent for bold styles, else the regular one. The spinner
text is in the default font.

## Not ported yet

The preferences behind the Game Setup spinners, horizontal scroll bars,
and dragging list rows with the right button (the list's drag pointer,
150, belongs to it). Pointers are in [cursors.md](cursors.md).

Open: the spinner text x (two readings of the original disagree),
the text edit baseline and selection colors, and which real keys the
original maps to its key codes 1002 to 1012.
