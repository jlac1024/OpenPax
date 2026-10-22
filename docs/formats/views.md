# Window layouts (VIEW, Data/ViewData)

32 window layouts. A window is created from one by the window constructor
(view id, palette index, parent); the palette comes from the
caller, not the data. Little endian; strings are int16 length + bytes.

## Structure

Every element is stored as its int32 class id followed by its fields
(generic object reader). There are no sizes, so an unknown
class cannot be skipped; the original stops with the view half read, and
the port refuses the view.

Shared base, seven int32: id, left, top (relative to the
window, also inside groups: group 31108 in VIEW 31000 is at 381,97 and its
buttons at 384,276), width, height, visible (0 hidden, 2 shown; a stored 1 becomes 2 on
load), enabled (0 disabled, 1 enabled, 2 enabled but held by the parent).

- Window, 8000: base; int16 child count; children; one byte
  read and ignored; title string.
- Group, 8200: base; int32 background picture (0 transparent);
  int16 child count; children.

## Widgets

Pictures are PPIC ids (id + 100 pressed or checked, id + 200 disabled).
Sounds play id on press and id + 100 on release. Text is a STR# list and
index. Field-by-field layouts, with the original's read addresses, are
on the record types in `src/OpenPax.Data/Views/`. Classes:

- 8002 button, 8039 animated button, 8009 picture, 8016 check box,
  8017 text entry, 8019 list, 8021 hot spot, 8024 wrapped text, 8025
  label, 8027 spinner, 8028 text line, 8033 scroll bar, 8034 column
  headings, 8035 multi-state toggle, 8037 animation, 8041 volume slider,
  8042 empire toggle.
- One byte only: 9000, 10002, 10003, 18001.
- Base only (drawn by code): 8038, 8040, 9002, 10001, 13002, 14001, 14900,
  18002, 19500, 19650, 19800, 22002, 24001, 24002, 26001, 26002.

## Quirks the port keeps

- Resource 31001 holds a window whose own id is 30000.
- Ids repeat in views 11300 and 32000; lookup by id returns the first.
- A volume slider's stored value is replaced on load by the current volume.
- A text entry stops taking characters when length equals the maximum.
- Column heading cells are stored as width and height, used as right and
  bottom.

Open: the scroll bar's constant 1100, the animation byte after its frame
rate, the list's second flag, the heading cell's flag byte, and what the
code-drawn panels show.
