# Converted data

What the importer (`openpax --import GAME OUT`, run by `build.sh`)
writes to `build/data/`, which `build.sh` copies to `deploy/data/`. The
GOG install is checked against the known hashes first; anything else is
refused.

## Layout

```
art/<image file>/<id>.png     one per picture, e.g. art/main/10000.png
art/<image file>/pictures.toml   index of that folder
palettes/<id>.pal             the 16 Data/PalData palettes, JASC-PAL text
tables/directions.toml        sine and cosine by degree from the executable (sim/movement.md)
tables/combat-sprites.toml    last visible row, width and height of each Combat.img picture (sim/combat.md, sim/combat-step.md)
fonts/                        each pft font as a PNG sheet with its metrics (fonts.md)
movies/<name>.mp4             each .smk movie as H.264 and AAC, with an index (decisions.md)
original/Data/, Sound/, MUSIC/   copied as they are until their readers land
import.toml                   import version and GOG build
```

## Pictures

- 8-bit indexed PNG. Pixel values are the original palette indices,
  unchanged.
- Index 1 is fully transparent (alpha 0 in the PNG's `tRNS` chunk), as in
  the original, where index 1 is never drawn. All other indices are opaque.
- The PNG's palette is one of the 16 game palettes, for viewing and
  editing only. In the game a picture has no palette of its own: the
  window that shows it was created with a palette (0 to 15), and the port
  draws with that, as the original does. So an edited PNG must keep its
  indices meaningful in the game palette, not just look right in a viewer.
- Repeated ids: `Image/Main.img` holds two pictures with id 27000. The
  original's lookup by id always returns the first, so it is `27000.png`
  and the second is `27000.1.png`, marked `unreachable_by_id`.

### Which palette is embedded

Nothing in the data says which palette a picture is meant for, so the
importer measures it. Under the right palette neighboring pixels are
much more alike than random pairs of pixels; under a wrong one the indices
become noise. Small sprites and one-color masks say little alone, so
measurements are pooled over each block of 50 consecutive ids (pictures
with nearby ids belong to the same screen), and a picture only keeps its
own choice when it is clearly better than its group's.

Examples: advisor portraits N000 use palette 1004, the N050 set 1005,
the N100 set 1002. Most combat sprites use 1005.

`pictures.toml` records the choice per picture:

| Key | Meaning |
|---|---|
| `id`, `file` | Resource id and PNG name |
| `left`, `top`, `width`, `height` | The picture's rectangle (left and top are 0 in all GOG data) |
| `palette` | Embedded palette (Data/PalData id) |
| `palette_differs_from_group` | Present when this picture overrode its group's palette |
| `palette_uncertain`, `palette_runner_up` | The group's choice was close and the runner-up would show this picture differently |
| `unreachable_by_id` | A repeated id that lookup by id never returns |

Palettes 1002 and 1011 differ in only 13 entries, so many close calls are
between those two and barely visible. When the screens that use each
picture are known from the `VIEW` data, the measured choice can be
replaced by the real one.
